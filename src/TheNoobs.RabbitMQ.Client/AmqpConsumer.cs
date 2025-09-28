using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Client.Abstractions;
using TheNoobs.RabbitMQ.Client.OpenTelemetry;
using TheNoobs.Results;
using TheNoobs.Results.Abstractions;
using TheNoobs.Results.Extensions;
using TheNoobs.Results.Types;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Client;

public class AmqpConsumer : AsyncDefaultBasicConsumer, IAsyncDisposable
{
    private bool _disposed;
    private readonly IChannel _channel;
    private string? _consumerId;
    private readonly IAmqpSerializer _serializer;
    private readonly IAmqpConsumerConfiguration _consumerConfiguration;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OpenTelemetryPropagator? _openTelemetryPropagator;

    public AmqpConsumer(
        IChannel channel,
        IAmqpSerializer serializer,
        IAmqpConsumerConfiguration consumerConfiguration,
        IServiceScopeFactory serviceScopeFactory,
        OpenTelemetryPropagator? openTelemetryPropagator) : base(channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _consumerConfiguration = consumerConfiguration ?? throw new ArgumentNullException(nameof(consumerConfiguration));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _openTelemetryPropagator = openTelemetryPropagator;
    }

    public override async Task HandleBasicDeliverAsync(
        string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        IReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = _openTelemetryPropagator?.StartActivity(properties);
            
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService(_consumerConfiguration.HandlerType);
            var request = _serializer.Deserialize(_consumerConfiguration.RequestType, body.Span);
            if (!request.IsSuccess)
            {
                await SendToDeadLetterAsync(properties, body, request.Fail);
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }
            
            var method = handler.GetType().GetMethod(nameof(IAmqpConsumer<object>.HandleAsync), [
                request.Value.GetType(),
                typeof(CancellationToken)
            ]);

            if (method is null)
            {
                var fail = new ServerErrorFail("Failed to find method to handle message");
                await SendToDeadLetterAsync(properties, body, fail);
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                
                return;
            }
            
            var invokeResult = method.Invoke(handler, [request.Value, cancellationToken])!;
            
            IResult result = invokeResult switch
            {
                ValueTask<Result<Void>> valueTaskResult => await valueTaskResult,
                ValueTask<Result<object>> objectValueTaskResult => await objectValueTaskResult,
                _ => await (dynamic)invokeResult
            };
            
            if (properties.IsReplyToPresent())
            {
                await ReplyAsync(properties, result, cancellationToken);
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }
            
            if (result.IsSuccess)
            {
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }

            if (_consumerConfiguration.Retry is not null)
            {
                var retryResult = await RetryAsync(_consumerConfiguration.Retry, properties, body, result.Fail);

                await (retryResult.IsSuccess
                    ? _channel.BasicAckAsync(deliveryTag, false, cancellationToken)
                    : _channel.BasicNackAsync(deliveryTag, false, true, cancellationToken));

                return;
            }
            
            await SendToDeadLetterAsync(properties, body, result.Fail);
            await _channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
        }
        catch
        {
            await _channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
        }
    }

    public async ValueTask<Result<Void>> StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _consumerId =
                await _channel.BasicConsumeAsync(_consumerConfiguration.QueueName, false, this, cancellationToken);
            return Void.Value;
        }
        catch (Exception e)
        {
            return new ServerErrorFail("Failed to start consumer", exception: e);
        }
    }
    
    public async ValueTask<Result<Void>> StopAsync()
    {
        if (string.IsNullOrWhiteSpace(_consumerId))
        {
            return Void.Value;
        }

        try
        {
            await _channel.BasicCancelAsync(_consumerId);
            return Void.Value;
        }
        catch (Exception e)
        {
            return new ServerErrorFail("Failed to stop consumer", exception: e);
        }
    }

    private async ValueTask ReplyAsync(IReadOnlyBasicProperties properties, IResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(properties.ReplyTo))
        {
            return;
        }

        try
        {
            var basicProperties = new BasicProperties();
            basicProperties.CorrelationId = properties.CorrelationId;
            _openTelemetryPropagator?.Propagate(basicProperties);
            
            var response = RpcResponse.Create(result, _serializer)
                .Bind(x => _serializer.Serialize(x.Value));

            await _channel.BasicPublishAsync(
                AmqpExchangeName.Direct,
                properties.ReplyTo, true,
                basicProperties,
                response.Value,
                cancellationToken);
        }
        catch
        {
            // TODO: Log error
        }
    }

    private async ValueTask<Result<Void>> RetryAsync(IAmqpRetry retry, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, Fail fail)
    {
        if (properties.Headers is null
            || !properties.Headers.TryGetValue("x-attempt", out var value)
            || value is not int attempt)
        {
            attempt = 1;
        }

        var basicProperties = new BasicProperties(properties);
        
        var retryResult = retry.GetNextDelay(attempt);
        if (retryResult.IsSuccess)
        {
            var scheduleQueueName = _consumerConfiguration.QueueName.ScheduledQueueName(retryResult.Value);
            
            var arguments = new Dictionary<string, object?>();
            
            arguments.Add("x-dead-letter-exchange", "");
            arguments.Add("x-dead-letter-routing-key", _consumerConfiguration.QueueName.Value);
            arguments.Add("x-message-ttl", (int)retryResult.Value.TotalMilliseconds);
            
            await _channel.QueueDeclareAsync(scheduleQueueName.Value, true, false,
                false, arguments);
            basicProperties.Headers ??= new Dictionary<string, object?>();
            basicProperties.Headers.Add("x-attempt", attempt + 1);
            await _channel.BasicPublishAsync(
                AmqpExchangeName.Direct,
                scheduleQueueName.Value,
                true,
                basicProperties,
                body);
            return Void.Value;
        }

        if (retryResult.Fail is NoAttemptsAvailable && retry.SendToDeadLetter)
        {
            await SendToDeadLetterAsync(properties, body, fail);
            return Void.Value;
        }

        if (retryResult.Fail is NoAttemptsAvailable && !retry.SendToDeadLetter)
        {
            return Void.Value;
        }

        return retryResult.Fail;
    }

    private async ValueTask SendToDeadLetterAsync(IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, Fail fail)
    {
        var dlqQueue = _consumerConfiguration.QueueName.DeadLetterQueueName().Value;
        
        await _channel.QueueDeclareAsync(dlqQueue, true, false,
            false);

        var basicProperties = new BasicProperties(properties);
        basicProperties.Headers ??= new Dictionary<string, object?>();
        basicProperties.Headers.Remove("x-dlq-reason");
        basicProperties.Headers.Add("x-dlq-reason", fail.Message);
        await _channel.BasicPublishAsync(
            AmqpExchangeName.Direct,
            dlqQueue,
            true,
            basicProperties,
            body);
    }

    public static ValueTask<Result<AmqpConsumer>> CreateAsync(
        IAmqpConnectionFactory connectionFactory,
        IAmqpSerializer serializer,
        IAmqpConsumerConfiguration consumerConfiguration,
        IServiceScopeFactory serviceScopeFactory,
        OpenTelemetryPropagator? openTelemetryPropagator,
        CancellationToken cancellationToken)
    {
        return connectionFactory.CreateConnectionAsync(cancellationToken)
            .BindAsync(x => CreateChannelAsync(x.Value, cancellationToken))
            .BindAsync(x => CreateQueueAsync(x.Value, consumerConfiguration, cancellationToken))
            .BindAsync<Void, AmqpConsumer>(x => new AmqpConsumer(
                x.GetValue<IChannel>().Value,
                serializer,
                consumerConfiguration,
                serviceScopeFactory,
                openTelemetryPropagator));
    }

    private static async ValueTask<Result<Void>> CreateQueueAsync(IChannel channel,
        IAmqpConsumerConfiguration consumerConfiguration, CancellationToken cancellationToken)
    {
        if (!consumerConfiguration.QueueName.AutoDeclare)
        {
            return Void.Value;
        }
        
        try
        {
            await channel.QueueDeclareAsync(consumerConfiguration.QueueName, true, false, false, cancellationToken: cancellationToken);
            foreach (var binding in consumerConfiguration.Bindings)
            {
                await channel.QueueBindAsync(consumerConfiguration.QueueName.Value, binding.ExchangeName, binding.RoutingKey, cancellationToken: cancellationToken);
            }
            return Void.Value;
        }
        catch (Exception ex)
        {
            return new ServerErrorFail("Failed to create the queue", exception: ex);
        }
    }

    private static async ValueTask<Result<IChannel>> CreateChannelAsync(IConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(true, true),
                cancellationToken);
            return new Result<IChannel>(channel);
        }
        catch (Exception ex)
        {
            return new ServerErrorFail("Failed to create channel for consumer", exception: ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            await StopAsync();
            await _channel.DisposeAsync();
            GC.SuppressFinalize(this);
        }
        catch
        {
            // ignore dispose error
        }
    }
}

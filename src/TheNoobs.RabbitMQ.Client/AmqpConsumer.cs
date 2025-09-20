using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Client.Abstractions;
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

    public AmqpConsumer(
        IChannel channel,
        IAmqpSerializer serializer,
        IAmqpConsumerConfiguration consumerConfiguration,
        IServiceScopeFactory serviceScopeFactory) : base(channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _consumerConfiguration = consumerConfiguration ?? throw new ArgumentNullException(nameof(consumerConfiguration));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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

            var result = await (ValueTask<Result<Void>>)method.Invoke(handler, [request.Value, cancellationToken])!;
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

            switch (result.Fail)
            {
                case ServerErrorFail:
                case ThirdPartyServiceErrorFail:
                case ResourceLockedFail:
                    await _channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
                    return;
                default:
                    await SendToDeadLetterAsync(properties, body, result.Fail);
                    return;
            }
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
            arguments.Add("x-message-ttl", retryResult.Value.TotalMilliseconds);
            
            await _channel.QueueDeclareAsync(scheduleQueueName.Value, true, false,
                false, arguments);
            basicProperties.Headers ??= new Dictionary<string, object?>();
            basicProperties.Headers.Add("x-attempt", attempt + 1);
            await _channel.BasicPublishAsync(
                "",
                scheduleQueueName.Value,
                true,
                basicProperties,
                body);
            return Void.Value;
        }

        if (retryResult.Fail is BadRequestFail && retry.SendToDeadLetter)
        {
            await SendToDeadLetterAsync(properties, body, fail);
            return Void.Value;
        }

        if (retryResult.Fail is BadRequestFail && !retry.SendToDeadLetter)
        {
            return Void.Value;
        }

        return retryResult.Fail;
    }

    private async ValueTask SendToDeadLetterAsync(IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, Fail fail)
    {
        await _channel.QueueDeclareAsync(_consumerConfiguration.QueueName.DeadLetterQueueName(), true, false,
            false);

        var basicProperties = new BasicProperties(properties);
        basicProperties.Headers ??= new Dictionary<string, object?>();
        basicProperties.Headers.Remove("x-dlq-reason");
        basicProperties.Headers.Add("x-dlq-reason", fail.Message);
        await _channel.BasicPublishAsync(
            "",
            _consumerConfiguration.QueueName.DeadLetterQueueName(),
            true,
            basicProperties,
            body);
    }

    public static ValueTask<Result<AmqpConsumer>> CreateAsync(
        IAmqpConnectionFactory connectionFactory,
        IAmqpSerializer serializer,
        IAmqpConsumerConfiguration consumerConfiguration,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
    {
        return connectionFactory.CreateConnectionAsync(cancellationToken)
            .BindAsync(x => CreateChannelAsync(x.Value, cancellationToken))
            .BindAsync(x => CreateQueueAsync(x.Value, consumerConfiguration, cancellationToken))
            .BindAsync<Void, AmqpConsumer>(x => new AmqpConsumer(x.GetValue<IChannel>().Value, serializer, consumerConfiguration, serviceScopeFactory));
    }

    private static async ValueTask<Result<Void>> CreateQueueAsync(IChannel channel,
        IAmqpConsumerConfiguration consumerConfiguration, CancellationToken cancellationToken)
    {
        try
        {
            await channel.QueueDeclareAsync(consumerConfiguration.QueueName, true, false, false, cancellationToken: cancellationToken);
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

using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;
using TheNoobs.Results.Abstractions;
using TheNoobs.Results.Extensions;
using TheNoobs.Results.Types;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ;

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
            var consumerType = _consumerConfiguration.HandlerType.GetInterface(typeof(IAmqpConsumer<,>).Name)!;
            var requestType = consumerType.GenericTypeArguments.First();
            var handler = scope.ServiceProvider.GetRequiredService(consumerType);
            var request = _serializer.Deserialize(requestType, body.Span);
            if (!request.IsSuccess)
            {
                await SendToDeadLetterAsync(properties, body, request.Fail);
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }
            
            var amqpRequestType = typeof(IAmqpMessage<>).MakeGenericType(requestType);
            var method = handler.GetType().GetMethod("HandleAsync", [
                amqpRequestType,
                typeof(CancellationToken)
            ]);

            if (method is null)
            {
                var fail = new ServerErrorFail("Failed to find method to handle message");
                await SendToDeadLetterAsync(properties, body, fail);
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                
                return;
            }
            
            var amqpMessageType = typeof(AmqpMessage<>).MakeGenericType(requestType);
            var amqpMessage = Activator.CreateInstance(amqpMessageType, request.Value, properties.Headers ?? new Dictionary<string, object?>());
            var invokeResult = method.Invoke(handler, [amqpMessage, cancellationToken])!;
            
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

            if (result.Fail is not NoRetryFail)
            {
                await _channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }
            
            await SendToDeadLetterAsync(properties, body, result.Fail);
            await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        }
        catch
        {
            await _channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
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

    private async ValueTask SendToDeadLetterAsync(IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, Fail fail)
    {
        var dlqQueue = _consumerConfiguration.QueueName.DeadLetterQueueName();

        var basicProperties = new BasicProperties(properties);
        basicProperties.Headers = new Dictionary<string, object?>(properties.Headers ?? new Dictionary<string, object?>())
        {
            ["x-dlq-reason"] = fail.Message
        };
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
        CancellationToken cancellationToken)
    {
        return connectionFactory.CreateConnectionAsync(cancellationToken)
            .BindAsync(x => CreateChannelAsync(x.Value, cancellationToken))
            .BindAsync(x => CreateQueueTopologyAsync(x.Value, consumerConfiguration, cancellationToken))
            .BindAsync<Void, AmqpConsumer>(x => new AmqpConsumer(
                x.GetValue<IChannel>().Value,
                serializer,
                consumerConfiguration,
                serviceScopeFactory));
    }

    private static async ValueTask<Result<Void>> CreateQueueTopologyAsync(IChannel channel,
        IAmqpConsumerConfiguration consumerConfiguration, CancellationToken cancellationToken)
    {
        if (!consumerConfiguration.QueueName.AutoDeclare)
        {
            return Void.Value;
        }
        
        try
        {
            var deadLetterQueueName = consumerConfiguration.QueueName.DeadLetterQueueName();
            await channel.QueueDeclareAsync(
                deadLetterQueueName,
                true,
                false,
                false,
                cancellationToken: cancellationToken);
            
            var scheduleQueueName = consumerConfiguration.QueueName.ScheduledQueueName(consumerConfiguration.RetryDelay);
            await channel.QueueDeclareAsync(
                scheduleQueueName,
                true,
                false,
                false,
                new Dictionary<string, object?>()
                {
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = consumerConfiguration.QueueName.Value,
                    ["x-message-ttl"] = (int)consumerConfiguration.RetryDelay.TotalMilliseconds
                },
                cancellationToken: cancellationToken);
                
            await channel.QueueDeclareAsync(
                consumerConfiguration.QueueName,
                true,
                false,
                false,
                new Dictionary<string, object?>()
                {
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = scheduleQueueName.Value,
                },
                cancellationToken: cancellationToken);
            
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

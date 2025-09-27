using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Client.Abstractions;
using TheNoobs.Results;
using TheNoobs.Results.Abstractions;
using TheNoobs.Results.Extensions;
using TheNoobs.Results.Types;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Client;

public class AmqpPublisher : IAmqpPublisher
{
    private readonly IAmqpConnectionFactory _connectionFactory;
    private readonly IAmqpSerializer _serializer;

    public AmqpPublisher(
        IAmqpConnectionFactory connectionFactory,
        IAmqpSerializer serializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }
    
    public ValueTask<Result<Void>> PublishAsync<T>(
        AmqpExchangeName exchangeName,
        AmqpRoutingKey routingKey,
        AmqpMessage<T> message,
        CancellationToken cancellationToken)
        where T : notnull
    {
        return _connectionFactory.CreateConnectionAsync(cancellationToken)
            .BindAsync(x => CreateChannelAsync(x.Value, cancellationToken))
            .BindAsync(x => PublishAsync(x.Value, exchangeName, routingKey, message, cancellationToken))
            .TapAsync(DisposeChannelAsync);
    }

    public ValueTask<Result<TOut>> SendAsync<TIn, TOut>(
        AmqpExchangeName amqpExchangeName,
        AmqpRoutingKey amqpRoutingKey,
        AmqpMessage<TIn> amqpMessage,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken) where TIn : notnull where TOut : notnull
    {
        return _connectionFactory.CreateConnectionAsync(cancellationToken)
            .BindAsync(x => CreateChannelAsync(x.Value, cancellationToken))
            .BindAsync(x => SendAsync<TIn, TOut>(
                x.Value,
                amqpExchangeName,
                amqpRoutingKey,
                amqpMessage,
                waitTimeout,
                cancellationToken: cancellationToken));
    }

    private static async ValueTask<Result<IChannel>> CreateChannelAsync(IConnection connection,
        CancellationToken cancellationToken)
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
            return new ServerErrorFail("Failed to create channel", exception: ex);
        }
    }

    private static async ValueTask<Result<Void>> DisposeChannelAsync(IResult result)
    {
        try
        {
            await result.GetValue<IChannel>().Value.DisposeAsync();
        }
        catch
        {
            // ignore dispose error
        }
        return Void.Value;
    }
    
    private ValueTask<Result<TOut>> SendAsync<T, TOut>(
        IChannel channel,
        AmqpExchangeName exchangeName,
        AmqpRoutingKey routingKey,
        AmqpMessage<T> message,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
        where T : notnull
        where TOut : notnull
    {
        return DeclareExchangeAsync(channel, exchangeName, cancellationToken)
            .BindAsync(_ => _serializer.Serialize(message.Value))
            .BindAsync(SendMessageAsync)
            .BindAsync(x => _serializer.Deserialize(typeof(TOut), x.Value))
            .BindAsync(x => new Result<TOut>((TOut)x.Value))
            .TapAsync(DisposeChannelAsync);

        async ValueTask<Result<byte[]>> SendMessageAsync(Result<byte[]> result)
        {
            try
            {
                var properties = new BasicProperties();
                properties.CorrelationId = message.CorrelationId;
                var replyQueue = await channel.QueueDeclareAsync(
                    Guid.NewGuid().ToString(),
                    exclusive: true,
                    autoDelete: true,
                    cancellationToken: cancellationToken);
                properties.ReplyTo = replyQueue.QueueName;
                await channel.BasicPublishAsync(
                    exchangeName,
                    routingKey,
                    true,
                    properties,
                    result.Value,
                    cancellationToken);

                var response = new ReadOnlyMemory<byte>();
                var semaphore = new SemaphoreSlim(0);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, deliverEventArgs) =>
                {
                    response = deliverEventArgs.Body.ToArray();
                    semaphore.Release();
                    return Task.CompletedTask;
                };
                
                await channel.BasicConsumeAsync(replyQueue.QueueName, true, consumer, cancellationToken);
                
                await semaphore.WaitAsync(waitTimeout, cancellationToken);
                
                var rpcResponse = (RpcResponse)_serializer.Deserialize(typeof(RpcResponse), response.Span);
                if (!rpcResponse.IsSuccess)
                {
                    return new ServerErrorFail(rpcResponse.Fail.Message, rpcResponse.Fail.Code, exception: rpcResponse.Fail.Exception);
                }
                return Convert.FromBase64String(rpcResponse.Value);
            } catch (Exception e)
            {
                return new ServerErrorFail("Failed to send message", exception: e);
            }
        }
    }

    private ValueTask<Result<Void>> PublishAsync<T>(
        IChannel channel,
        AmqpExchangeName exchangeName,
        AmqpRoutingKey routingKey,
        AmqpMessage<T> message,
        CancellationToken cancellationToken)
        where T : notnull
    {
        return DeclareExchangeAsync(channel, exchangeName, cancellationToken)
            .BindAsync(_ => _serializer.Serialize(message.Value))
            .BindAsync(PublishMessageAsync);

        async ValueTask<Result<Void>> PublishMessageAsync(Result<byte[]> result)
        {
            try
            {
                await channel.BasicPublishAsync(
                    exchangeName,
                    routingKey,
                    result.Value,
                    cancellationToken);
                return Void.Value;
            } catch (Exception e)
            {
                return new ServerErrorFail("Failed to publish message", exception: e);
            }
        }
    }
    
    async ValueTask<Result<Void>> DeclareExchangeAsync(IChannel channel, AmqpExchangeName exchangeName, CancellationToken cancellationToken)
    {
        if (!exchangeName.AutoDeclare)
        {
            return Void.Value;
        }
            
        try
        {
            await channel.ExchangeDeclareAsync(
                exchangeName.Value,
                ParseExchangeType(exchangeName.Type),
                true,
                cancellationToken: cancellationToken);
            return Void.Value;
        }
        catch (Exception e)
        {
            return new ServerErrorFail("Failed to declare exchange", exception: e);
        }
    }
    
    private static string ParseExchangeType(AmqpExchangeType exchangeType)
    {
        return exchangeType switch
        {
            AmqpExchangeType.DIRECT => ExchangeType.Direct,
            AmqpExchangeType.FANOUT => ExchangeType.Fanout,
            AmqpExchangeType.HEADERS => ExchangeType.Headers,
            AmqpExchangeType.TOPIC => ExchangeType.Topic,
            _ => throw new ArgumentOutOfRangeException(nameof(exchangeType), exchangeType, null)
        };
    }
}

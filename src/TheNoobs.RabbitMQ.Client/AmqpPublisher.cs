using System.Text;
using RabbitMQ.Client;
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

    private ValueTask<Result<Void>> PublishAsync<T>(
        IChannel channel,
        AmqpExchangeName exchangeName,
        AmqpRoutingKey routingKey,
        AmqpMessage<T> message,
        CancellationToken cancellationToken)
        where T : notnull
    {
        return _serializer.Serialize(message.Value)
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
}

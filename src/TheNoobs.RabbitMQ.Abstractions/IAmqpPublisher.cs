using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpPublisher
{
    ValueTask<Result<TheNoobs.Results.Types.Void>> PublishAsync<T>(
        AmqpExchangeName amqpExchangeName,
        AmqpRoutingKey amqpRoutingKey,
        AmqpMessage<T> amqpMessage,
        CancellationToken cancellationToken)
        where T : notnull;
}

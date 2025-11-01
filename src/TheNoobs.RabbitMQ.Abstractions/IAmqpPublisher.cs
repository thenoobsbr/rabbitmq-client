using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpPublisher
{
    ValueTask<Result<TheNoobs.Results.Types.Void>> PublishAsync<T>(
        AmqpExchangeName amqpExchangeName,
        AmqpRoutingKey amqpRoutingKey,
        T amqpMessage,
        CancellationToken cancellationToken)
        where T : notnull;
    
    
    ValueTask<Result<TOut>> SendAsync<T, TOut>(
        AmqpExchangeName amqpExchangeName,
        AmqpRoutingKey amqpRoutingKey,
        T amqpMessage,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
        where T : notnull
        where TOut : notnull;
}

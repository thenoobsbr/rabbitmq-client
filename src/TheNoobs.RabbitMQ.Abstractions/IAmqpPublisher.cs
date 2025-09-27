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
    
    
    ValueTask<Result<TOut>> SendAsync<TIn, TOut>(
        AmqpExchangeName amqpExchangeName,
        AmqpRoutingKey amqpRoutingKey,
        AmqpMessage<TIn> amqpMessage,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
        where TIn : notnull
        where TOut : notnull;
}

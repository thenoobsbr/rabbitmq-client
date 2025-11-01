using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public delegate ValueTask<Result<TOut>> AmqpPipelineDelegate<T, TOut>(IAmqpMessage<T> message, CancellationToken cancellationToken)
    where T : notnull
    where TOut : notnull;

public interface IAmqpConsumerPipeline<T, TOut>
    where T : notnull
    where TOut : notnull
{
    ValueTask<Result<TOut>> HandleAsync(IAmqpMessage<T> message, AmqpPipelineDelegate<T, TOut> next, CancellationToken cancellationToken);
}

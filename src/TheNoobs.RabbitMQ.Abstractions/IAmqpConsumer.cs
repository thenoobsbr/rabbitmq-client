using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpConsumer<T, TOut>
    where T : notnull
    where TOut : notnull
{
    ValueTask<Result<TOut>> HandleAsync(IAmqpMessage<T> message, CancellationToken cancellationToken);
}

using TheNoobs.Results;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpConsumer<in T, TOut>
    where T : notnull
    where TOut : notnull
{
    ValueTask<Result<TOut>> HandleAsync(T message, CancellationToken cancellationToken);
}

public interface IAmqpConsumer<in T> : IAmqpConsumer<T, Void>
    where T : notnull;

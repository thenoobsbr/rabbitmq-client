using TheNoobs.Results;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpConsumer<in T>
    where T : notnull
{
    ValueTask<Result<Void>> HandleAsync(T message, CancellationToken cancellationToken);
}

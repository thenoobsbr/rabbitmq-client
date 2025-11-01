using RabbitMQ.Client;
using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpConnectionFactory
{
    ValueTask<Result<IConnection>> CreateConnectionAsync(CancellationToken cancellationToken);
}

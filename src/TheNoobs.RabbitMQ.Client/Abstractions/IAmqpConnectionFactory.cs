using RabbitMQ.Client;
using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Client.Abstractions;

public interface IAmqpConnectionFactory
{
    ValueTask<Result<IConnection>> CreateConnectionAsync(CancellationToken cancellationToken);
}

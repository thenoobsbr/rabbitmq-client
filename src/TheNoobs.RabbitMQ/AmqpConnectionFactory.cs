using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ;

public class AmqpConnectionFactory : IAmqpConnectionFactory
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private IConnection? _connection;
    private readonly IConnectionFactory _connectionFactory;

    public AmqpConnectionFactory(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }
    
    public async ValueTask<Result<IConnection>> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return new Result<IConnection>(_connection);
        }

        try
        {
            await _semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                _connection ??= await _connectionFactory.CreateConnectionAsync(cancellationToken);
                return new Result<IConnection>(_connection);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        } catch (Exception ex)
        {
            return new ServerErrorFail("Failed to connect to RabbitMQ", exception: ex);
        }
    }
}

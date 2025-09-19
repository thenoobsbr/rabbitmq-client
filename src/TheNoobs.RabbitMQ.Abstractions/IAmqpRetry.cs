using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpRetry
{
    Result<TimeSpan> GetNextDelay(int attempt);
}

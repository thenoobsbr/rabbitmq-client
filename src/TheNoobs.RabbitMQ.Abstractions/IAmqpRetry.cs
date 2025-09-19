using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpRetry
{
    bool SendToDeadLetter { get; }
    Result<TimeSpan> GetNextDelay(int attempt);
}

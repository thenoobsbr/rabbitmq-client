using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

public class AmqpLinearRetryAttribute : AmqpRetryAttribute
{
    public int DelayInSeconds { get; init; } = 10;
    public int MaxDelayInSeconds { get; init; } = 60;
    
    public override Result<TimeSpan> GetNextDelay(int attempt)
    {
        if (MaxAttempts is not null && attempt > MaxAttempts)
        {
            return new NoAttemptsAvailable();
        }
        
        return TimeSpan.FromSeconds(Math.Min(DelayInSeconds * attempt, MaxDelayInSeconds));
    }
}

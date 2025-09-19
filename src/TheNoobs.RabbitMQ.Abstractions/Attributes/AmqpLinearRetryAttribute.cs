using TheNoobs.RabbitMQ.Abstractions.Attributes;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions.RetryBehaviors;

public class AmqpLinearRetryAttribute : AmqpRetryAttribute
{
    public int DelayInSeconds { get; init; } = 10;
    public int MaxDelayInSeconds { get; init; } = 60;
    
    public override Result<TimeSpan> GetNextDelay(int attempt)
    {
        if (MaxAttempts is not null && attempt > MaxAttempts)
        {
            return new BadRequestFail("Retry count exceeded");
        }
        
        return TimeSpan.FromSeconds(Math.Min(DelayInSeconds * attempt, MaxDelayInSeconds));
    }
}

using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public abstract class AmqpRetryAttribute : Attribute, IAmqpRetry
{
    public int? MaxAttempts { get; init; } = 10;
    public bool FireAndForget { get; init; } = false;

    public abstract Result<TimeSpan> GetNextDelay(int attempt);
}

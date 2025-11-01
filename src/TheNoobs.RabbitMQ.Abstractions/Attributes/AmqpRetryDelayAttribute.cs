namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class AmqpRetryDelayAttribute : Attribute
{
    public AmqpRetryDelayAttribute(TimeSpan delay)
    {
        Delay = delay;
    }
    
    public TimeSpan Delay { get; }
}

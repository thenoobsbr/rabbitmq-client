using System.Text;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions;

public record AmqpQueueName
{
    private AmqpQueueName(string value)
    {
        Value = value;
    }
    
    public string Value { get; }

    public static implicit operator AmqpQueueName(string queueName) => Create(queueName);
    public static implicit operator string(AmqpQueueName queueName) => queueName.Value;

    public AmqpQueueName DeadLetterQueueName() => Create($"dlq-{Value}");
    public AmqpQueueName ScheduledQueueName(TimeSpan delay) => Create($"sch-{Value}-{delay.TotalSeconds}s");

    public static Result<AmqpQueueName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new InvalidInputFail("QueueName cannot be null or whitespace");
        }
        
        var byteLength = Encoding.UTF8.GetByteCount(value);
        if (byteLength > 256)
        {
            return new InvalidInputFail("QueueName cannot be longer than 256 bytes");
        }

        return new AmqpQueueName(value);
    }
}

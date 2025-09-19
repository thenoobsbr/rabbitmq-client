using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions;

public record AmqpMessage<T> where T : notnull
{
    private AmqpMessage(object value, TimeSpan? delay)
    {
        Value = value;
        Delay = delay;
    }

    public object Value { get; }
    public TimeSpan? Delay { get; }

    public static Result<AmqpMessage<T>> Create(T value, TimeSpan? delay = null)
    {
        if (ReferenceEquals(value, null))
        {
            return new InvalidInputFail("Message cannot be null");
        }
        
        return new AmqpMessage<T>(value, delay);
    }
}

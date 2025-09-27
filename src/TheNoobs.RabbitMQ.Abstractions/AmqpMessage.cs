using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions;

public record AmqpMessage<T> where T : notnull
{
    private AmqpMessage(object value, TimeSpan? delay, string correlationId)
    {
        Value = value;
        Delay = delay;
        CorrelationId = correlationId;
    }

    public object Value { get; }
    public TimeSpan? Delay { get; }
    public string CorrelationId { get; }

    public static Result<AmqpMessage<T>> Create(
        T value,
        TimeSpan? delay = null,
        string? correlationId = null)
    {
        if (ReferenceEquals(value, null))
        {
            return new InvalidInputFail("Message cannot be null");
        }
        
        return new AmqpMessage<T>(value, delay, correlationId ?? Guid.NewGuid().ToString());
    }
}

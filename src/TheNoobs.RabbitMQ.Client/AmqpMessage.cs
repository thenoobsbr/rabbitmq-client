using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ.Client;

public sealed class AmqpMessage<T> : IAmqpMessage<T>
where T : notnull
{
    public AmqpMessage(T value, IDictionary<string, object?> headers)
    {
        Value = value;
        Headers = headers;
    }
    
    public T Value { get; }
    public IDictionary<string, object?> Headers { get; }
}

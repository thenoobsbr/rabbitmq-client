namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpMessage<T>
where T : notnull
{
    T Value { get; }
    IDictionary<string, object?> Headers { get; }
}

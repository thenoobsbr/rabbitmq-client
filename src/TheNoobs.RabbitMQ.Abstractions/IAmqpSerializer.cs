using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpSerializer
{
    Result<byte[]> Serialize(object value);
    Result<object> Deserialize(Type type, ReadOnlySpan<byte> value);
}

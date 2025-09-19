using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpSerializer
{
    Result<byte[]> Serialize(object value);
    Result<T> Deserialize<T>(ReadOnlySpan<byte> value);
}

using System.Text.Json;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Client;

public class AmqpDefaultJsonSerializer : IAmqpSerializer
{
    private readonly JsonSerializerOptions _options;

    public AmqpDefaultJsonSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    public Result<byte[]> Serialize(object value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    public Result<T> Deserialize<T>(ReadOnlySpan<byte> value)
    {
        var result = JsonSerializer.Deserialize<T>(value, _options);
        if (ReferenceEquals(result, null))
        {
            return new ServerErrorFail("Failed to deserialize message");
        }
        return result;
    }
}

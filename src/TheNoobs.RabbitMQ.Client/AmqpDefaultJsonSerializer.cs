﻿using System.Text.Json;
using System.Text.Json.Serialization;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Client;

public class AmqpDefaultJsonSerializer : IAmqpSerializer
{
    private readonly JsonSerializerOptions _options;

    public AmqpDefaultJsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters = { new JsonStringEnumConverter() }
        };
    }
    public Result<byte[]> Serialize(object value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    public Result<object> Deserialize(Type type, ReadOnlySpan<byte> value)
    {
        var result = JsonSerializer.Deserialize(value, type, _options);
        if (ReferenceEquals(result, null))
        {
            return new ServerErrorFail("Failed to deserialize message");
        }
        return result;
    }
}

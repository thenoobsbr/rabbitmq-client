using System.Text;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions;

public record AmqpRoutingKey
{
    private AmqpRoutingKey(string value)
    {
        Value = value;
    }
    
    public string Value { get; }
    
    public static implicit operator AmqpRoutingKey(string value) => Create(value);
    public static implicit operator string(AmqpRoutingKey value) => value.Value;

    public static Result<AmqpRoutingKey> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new InvalidInputFail("RoutingKey cannot be null or whitespace");
        }
        
        var byteLength = Encoding.UTF8.GetByteCount(value);
        if (byteLength > 256)
        {
            return new InvalidInputFail("RoutingKey cannot be longer than 256 bytes");
        }

        return new AmqpRoutingKey(value);
    }
}

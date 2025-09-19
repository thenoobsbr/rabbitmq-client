using System.Text;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions;

public record AmqpExchangeName
{
    private AmqpExchangeName(string value)
    {
        Value = value;
    }
    
    public string Value { get; }
    
    public static implicit operator AmqpExchangeName(string value) => new(value);
    public static implicit operator string(AmqpExchangeName value) => value.Value;

    public static Result<AmqpExchangeName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new InvalidInputFail("ExchangeName cannot be null or whitespace");
        }
        
        var byteLength = Encoding.UTF8.GetByteCount(value);
        if (byteLength > 256)
        {
            return new InvalidInputFail("ExchangeName cannot be longer than 256 bytes");
        }

        return new AmqpExchangeName(value);
    }
}

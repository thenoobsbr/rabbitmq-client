using System.Text;
using TheNoobs.Results;
using TheNoobs.Results.Types;

namespace TheNoobs.RabbitMQ.Abstractions;

public record AmqpExchangeName
{
    private AmqpExchangeName(string value, AmqpExchangeType type, bool autoDeclare)
    {
        Value = value;
        Type = type;
        AutoDeclare = autoDeclare;
    }
    
    public string Value { get; }
    public AmqpExchangeType Type { get; }
    public bool AutoDeclare { get; }
    public static AmqpExchangeName Direct => new("", AmqpExchangeType.DIRECT, false);

    public static implicit operator AmqpExchangeName(string value) => new(value, AmqpExchangeType.TOPIC, true);
    public static implicit operator string(AmqpExchangeName value) => value.Value;

    public static Result<AmqpExchangeName> Create(string value, AmqpExchangeType type = AmqpExchangeType.TOPIC, bool autoDeclare = true)
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

        return new AmqpExchangeName(value, type, autoDeclare);
    }
}

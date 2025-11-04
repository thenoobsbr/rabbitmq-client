using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ.Extensions;

internal static class AmqpExchangeTypeExtensions
{
    internal static string ToRabbitMQExchangeType(this AmqpExchangeType exchangeType)
    {
        return exchangeType switch
        {
            AmqpExchangeType.DIRECT => ExchangeType.Direct,
            AmqpExchangeType.FANOUT => ExchangeType.Fanout,
            AmqpExchangeType.HEADERS => ExchangeType.Headers,
            AmqpExchangeType.TOPIC => ExchangeType.Topic,
            _ => throw new ArgumentOutOfRangeException(nameof(exchangeType), exchangeType, null)
        };
    }
    
}

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpQueueBinding
{
    AmqpExchangeName ExchangeName { get; }
    AmqpRoutingKey RoutingKey { get; }
}

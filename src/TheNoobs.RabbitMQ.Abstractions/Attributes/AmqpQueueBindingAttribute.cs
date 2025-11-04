namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AmqpQueueBindingAttribute : Attribute, IAmqpQueueBinding
{

    public AmqpQueueBindingAttribute(string exchangeName, string routingKey, AmqpExchangeType exchangeType = AmqpExchangeType.TOPIC)
    {
        ExchangeName = AmqpExchangeName.Create(exchangeName, exchangeType);
        RoutingKey = routingKey;
    }
    
    public AmqpExchangeName ExchangeName { get; }
    public AmqpRoutingKey RoutingKey { get; }
}

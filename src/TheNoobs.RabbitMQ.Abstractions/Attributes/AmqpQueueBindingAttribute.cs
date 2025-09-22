namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AmqpQueueBindingAttribute : Attribute, IAmqpQueueBinding
{

    public AmqpQueueBindingAttribute(string exchangeName, string routingKey)
    {
        ExchangeName = exchangeName;
        RoutingKey = routingKey;
    }
    
    public AmqpExchangeName ExchangeName { get; }
    public AmqpRoutingKey RoutingKey { get; }
}

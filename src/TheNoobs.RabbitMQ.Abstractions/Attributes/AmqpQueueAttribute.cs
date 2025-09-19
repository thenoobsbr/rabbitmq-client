namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AmqpQueueAttribute : Attribute
{

    public AmqpQueueAttribute(string queueName)
    {
        QueueName = queueName;
    }
    
    public AmqpQueueName QueueName { get; }
}

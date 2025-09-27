namespace TheNoobs.RabbitMQ.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class AmqpQueueAttribute : Attribute
{

    public AmqpQueueAttribute(string queueName, bool autoDeclare = true)
    {
        QueueName = AmqpQueueName.Create(queueName, autoDeclare).Value;
    }
    
    public AmqpQueueName QueueName { get; }
}

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpConsumerConfiguration
{
    Type HandlerType { get; }
    AmqpQueueName QueueName { get; }
    TimeSpan RetryDelay { get; }
    IAmqpQueueBinding[] Bindings { get; }
}

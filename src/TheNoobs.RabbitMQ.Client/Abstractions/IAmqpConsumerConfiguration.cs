using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ.Client.Abstractions;

public interface IAmqpConsumerConfiguration
{
    Type HandlerType { get; }
    AmqpQueueName QueueName { get; }
    TimeSpan RetryDelay { get; }
    IAmqpQueueBinding[] Bindings { get; }
}

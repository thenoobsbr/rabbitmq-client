using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ.Client.Abstractions;

public interface IAmqpConsumerConfiguration
{
    Type HandlerType { get; }
    Type RequestType { get; }
    AmqpQueueName QueueName { get; }
    IAmqpRetry? Retry { get; }
    IAmqpQueueBinding[] Bindings { get; }
}

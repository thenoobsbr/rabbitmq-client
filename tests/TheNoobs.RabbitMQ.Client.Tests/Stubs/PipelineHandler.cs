using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Client.Tests.Stubs;

public class PipelineHandler<T, TOut> : IAmqpConsumerPipeline<T, TOut>
    where T : notnull
    where TOut : notnull
{
    public Action<IAmqpMessage<T>, AmqpPipelineDelegate<T, TOut>, CancellationToken>? Action { get; set; } 
    
    public ValueTask<Result<TOut>> HandleAsync(IAmqpMessage<T> message, AmqpPipelineDelegate<T, TOut> next, CancellationToken cancellationToken)
    {
        Action?.Invoke(message, next, cancellationToken);
        return next(message, cancellationToken);
    }
}

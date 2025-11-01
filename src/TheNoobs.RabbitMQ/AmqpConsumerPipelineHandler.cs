using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;

namespace TheNoobs.RabbitMQ;

internal class AmqpConsumerPipelineHandler<T, TOut> : IAmqpConsumer<T, TOut>
    where T : notnull
    where TOut : notnull
{
    private readonly IAmqpConsumer<T, TOut> _consumer;
    private readonly IReadOnlyCollection<IAmqpConsumerPipeline<T, TOut>> _pipelines;

    public AmqpConsumerPipelineHandler(IAmqpConsumer<T, TOut> consumer, IEnumerable<IAmqpConsumerPipeline<T, TOut>> pipelines)
    {
        _consumer = consumer;
        _pipelines = pipelines.ToList();
    }

    public async ValueTask<Result<TOut>> HandleAsync(IAmqpMessage<T> message, CancellationToken cancellationToken)
    {
        AmqpPipelineDelegate<T, TOut> pipeline = _consumer.HandleAsync;
        
        foreach (var middleware in _pipelines.Reverse())
        {
            var next = pipeline;
            var currentMiddleware = middleware;
            pipeline = (msg, ct) => currentMiddleware.HandleAsync(msg, next, ct);
        }
        
        return await pipeline(message, cancellationToken);
    }
}

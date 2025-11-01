using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;

namespace TheNoobs.RabbitMQ.Tests.Stubs;

public abstract class StubHandler<TResponse> : IAmqpConsumer<StubMessage, TResponse>
    where TResponse : notnull
{
    private readonly Func<StubMessage, CancellationToken, ValueTask<Result<TResponse>>> _handler;

    public StubHandler(Func<StubMessage, CancellationToken, ValueTask<Result<TResponse>>> handler)
    {
        _handler = handler;
    }
    public ValueTask<Result<TResponse>> HandleAsync(IAmqpMessage<StubMessage> message, CancellationToken cancellationToken)
    {
        return _handler(message.Value, cancellationToken);
    }
}

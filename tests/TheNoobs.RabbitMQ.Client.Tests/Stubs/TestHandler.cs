using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;
using TheNoobs.Results.Abstractions;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Client.Tests.Stubs;

public class StubHandler<TResponse> : IAmqpConsumer<StubMessage, TResponse>
    where TResponse : notnull
{
    private readonly Func<StubMessage, CancellationToken, ValueTask<Result<TResponse>>> _handler;

    public StubHandler(Func<StubMessage, CancellationToken, ValueTask<Result<TResponse>>> handler)
    {
        _handler = handler;
    }
    public ValueTask<Result<TResponse>> HandleAsync(StubMessage message, CancellationToken cancellationToken)
    {
        return _handler(message, cancellationToken);
    }
}

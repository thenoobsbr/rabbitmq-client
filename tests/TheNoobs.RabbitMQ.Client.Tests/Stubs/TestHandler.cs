using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Client.Tests.Stubs;

public class StubHandler : IAmqpConsumer<StubMessage>
{
    private readonly Func<StubMessage, CancellationToken, ValueTask<Result<Void>>> _handler;

    public StubHandler(Func<StubMessage, CancellationToken, ValueTask<Result<Void>>> handler)
    {
        _handler = handler;
    }
    public ValueTask<Result<Void>> HandleAsync(StubMessage message, CancellationToken cancellationToken)
    {
        return _handler(message, cancellationToken);
    }
}

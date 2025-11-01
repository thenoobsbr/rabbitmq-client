using TheNoobs.RabbitMQ.Abstractions.Attributes;
using TheNoobs.Results;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Client.Tests.Stubs;

[AmqpQueue("test")]
public class EventHandler : StubHandler<Void>
{
    public EventHandler() : base((_, _) => ValueTask.FromResult(new Result<Void>(Void.Value)))
    {
    }
    
    public EventHandler(Func<StubMessage, CancellationToken, ValueTask<Result<Void>>> handler) : base(handler)
    {
    }
}

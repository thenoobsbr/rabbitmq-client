using TheNoobs.Results.Abstractions;

namespace TheNoobs.RabbitMQ.Abstractions;

public record NoRetryFail : Fail
{
    public NoRetryFail(Fail fail) : base(fail)
    {
    }
    public NoRetryFail(string message, string code, Exception? exception) : base(message, code, exception)
    {
    }
}

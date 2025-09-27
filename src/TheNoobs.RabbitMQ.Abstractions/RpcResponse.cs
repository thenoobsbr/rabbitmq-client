using TheNoobs.Results;
using TheNoobs.Results.Abstractions;
using TheNoobs.Results.Extensions;

namespace TheNoobs.RabbitMQ.Abstractions;

public class RpcResponse
{
    public bool IsSuccess { get; init; }
    public string Value { get; init; } = string.Empty;
    public RpcFail Fail { get; init; } = null!;

    public static Result<RpcResponse> Create(IResult result, IAmqpSerializer serializer)
    {
        if (!result.IsSuccess)
        {
            return new RpcResponse()
            {
                IsSuccess = false,
                Fail = RpcFail.FromResult(result)!
            };
        }
        
        return serializer.Serialize(result.GetValue())
            .Bind<byte[], string>(x => Convert.ToBase64String(x.Value))
            .Bind<string, RpcResponse>(x => new RpcResponse()
            {
                IsSuccess = true,
                Value = x
            });
    }
    
    public class RpcFail
    {
        public string Message { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public Type Type { get; init; } = null!;
        public Exception? Exception { get; init; }

        public static RpcFail? FromResult(IResult result)
        {
            if (result.IsSuccess)
            {
                return null;
            }

            return new RpcFail
            {
                Code = result.Fail.Code,
                Message = result.Fail.Message,
                Exception = result.Fail.Exception,
                Type = result.Fail.GetType()
            };
        }
    }
}

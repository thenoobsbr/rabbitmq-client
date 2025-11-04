using System.Globalization;
using System.Text;
using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ;

public sealed class AmqpMessage<T> : IAmqpMessage<T>
where T : notnull
{
    public AmqpMessage(T value, IDictionary<string, object?> headers)
    {
        Value = value;
        Headers = headers.ToDictionary(x => x.Key, x => ConvertToString(x.Value));
    }
    
    public T Value { get; }
    public IDictionary<string, string?> Headers { get; }
    
    private static string? ConvertToString(object? value)
    {
        return value switch
        {
            int i => i.ToString(),
            long l => l.ToString(),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b.ToString(),
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString(),
            string s => s,
            char[] chars => new string(chars),
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => null
        };
    }
}

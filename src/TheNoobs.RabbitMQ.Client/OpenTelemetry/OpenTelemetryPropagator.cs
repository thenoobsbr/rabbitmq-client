using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;

namespace TheNoobs.RabbitMQ.Client.OpenTelemetry;

public class OpenTelemetryPropagator
{
    private readonly ActivitySource _activitySource;
    
    public const string TRACE_PARENT_HEADER = "traceparent";
    public const string TRACE_STATE_HEADER = "tracestate";

    public OpenTelemetryPropagator(ActivitySource activitySource)
    {
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
    }

    public Activity StartActivity(IReadOnlyBasicProperties properties)
    {
        const string ACTIVITY_NAME = "RabbitMQ Consumer";
        if (properties.Headers is null
            || !TryGetValue(properties.Headers, TRACE_PARENT_HEADER, out var traceparent)
            || !TryGetValue(properties.Headers, TRACE_STATE_HEADER, out var tracestate)
            || !ActivityContext.TryParse(traceparent, tracestate, out var activityContext))
        {
            return _activitySource.StartActivity(ACTIVITY_NAME, ActivityKind.Consumer)!;
        }

        return _activitySource.StartActivity(ACTIVITY_NAME, ActivityKind.Consumer, activityContext)!;
    }

    public void Propagate(BasicProperties basicProperties)
    {
        if (Activity.Current is null)
        {
            return;
        }
        
        basicProperties.Headers ??= new Dictionary<string, object?>();
        basicProperties.Headers.Add(TRACE_PARENT_HEADER, Activity.Current.Id);
        basicProperties.Headers.Add(TRACE_STATE_HEADER, Activity.Current.TraceStateString);
    }

    private bool TryGetValue(IDictionary<string, object?> properties, string key, out string value)
    {
        value = string.Empty;
        if (!properties.TryGetValue(key, out var propertyValue))
        {
            return false;
        }

        if (propertyValue is not byte[] bytes)
        {
            return false;
        }
        
        value = Encoding.UTF8.GetString(bytes);
        return true;
    }
}

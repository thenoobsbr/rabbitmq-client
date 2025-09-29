using System.Diagnostics;
using System.Reflection;
using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ.Client.Abstractions;

public interface IAmqpConfigurationBuilder
{
    IAmqpConfigurationBuilder UseConnectionString(string connectionString);
    IAmqpConfigurationBuilder UseSerializer<TSerializer>()
        where TSerializer : IAmqpSerializer;
    IAmqpConfigurationBuilder AddConsumersFromAssemblies(params Assembly[] assemblies);
    IAmqpConfigurationBuilder UseOpenTelemetry(ActivitySource source);
}

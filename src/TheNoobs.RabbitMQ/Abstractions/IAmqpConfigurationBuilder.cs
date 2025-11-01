using System.Reflection;
using TheNoobs.RabbitMQ.Abstractions;

namespace TheNoobs.RabbitMQ.Abstractions;

public interface IAmqpConfigurationBuilder
{
    IAmqpConfigurationBuilder UseConnectionString(string connectionString);
    IAmqpConfigurationBuilder UseSerializer<TSerializer>()
        where TSerializer : IAmqpSerializer;
    IAmqpConfigurationBuilder AddConsumersFromAssemblies(params Assembly[] assemblies);
    IAmqpConfigurationBuilder AddPipelinesFromAssemblies(params Assembly[] assemblies);
    IAmqpConfigurationBuilder AddConsumersAndPipelinesFromAssemblies(params Assembly[] assemblies);
    IAmqpConfigurationBuilder UseDefaultRetryDelay(TimeSpan delay);
}

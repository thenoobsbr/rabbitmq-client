using System.Reflection;

namespace TheNoobs.RabbitMQ.Client.Abstractions;

public interface IAmqpConfigurationBuilder
{
    string ConnectionString { get; set; }
    Type SerializerType { get; set; }
    void AddConsumersFromAssemblies(params Assembly[] assemblies);
}

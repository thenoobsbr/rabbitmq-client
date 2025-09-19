using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Abstractions.Attributes;
using TheNoobs.RabbitMQ.Client.Abstractions;

namespace TheNoobs.RabbitMQ.Client.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddRabbitMQClient(this IServiceCollection services,
        Action<IAmqpConfigurationBuilder> builder)
    {
        var amqpBuilder = new AmqpConfigurationBuilder();
        builder(amqpBuilder);
        var connectionFactory = new ConnectionFactory();
        connectionFactory.Uri = new Uri(amqpBuilder.ConnectionString);
        services.AddSingleton<IAmqpConnectionFactory>(new AmqpConnectionFactory(connectionFactory));
        services.AddSingleton<IAmqpPublisher, AmqpPublisher>();
        services.AddSingleton(typeof(IAmqpSerializer), amqpBuilder.SerializerType);
        return services.AddConsumers(amqpBuilder.Assemblies);
    }

    private static IServiceCollection AddConsumers(this IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        var consumerHandlers = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x =>
                x.GetInterface(typeof(IAmqpConsumer<>).Name) != null && x is { IsClass: true }
            )
            .ToList();
        foreach (var handler in consumerHandlers)
        {
            services.AddScoped(handler);
            var queueAttributes = handler.GetCustomAttributes<AmqpQueueAttribute>().ToList();
            if (!queueAttributes.Any())
            {
                throw new InvalidOperationException($"AmqpQueueAttribute not found in {handler.Name}");
            }

            var retryAttribute = handler.GetCustomAttribute<AmqpRetryAttribute>();
            foreach (var queueAttribute in queueAttributes)
            {
                services.AddSingleton(typeof(IAmqpConsumerConfiguration), new AmqpConsumerConfiguration(handler, retryAttribute, queueAttribute.QueueName));
            }
        }

        return services;
    }

    class AmqpConfigurationBuilder : IAmqpConfigurationBuilder
    {
        public string ConnectionString { get; set; } = string.Empty;
        public Type SerializerType { get; set; } = typeof(AmqpDefaultJsonSerializer);
        
        internal List<Assembly> Assemblies { get; set; } = new();
        
        public void AddConsumersFromAssemblies(params Assembly[] assemblies)
        {
            Assemblies.AddRange(assemblies);
        }
    }
    
    class AmqpConsumerConfiguration(Type handlerType, IAmqpRetry? retry, AmqpQueueName queueName)
    {
        public Type HandlerType => handlerType;
        public Type RequestType => handlerType.GetInterface(typeof(IAmqpConsumer<>).Name)!.GetGenericArguments().First();
        public AmqpQueueName QueueName => queueName;
        public IAmqpRetry? Retry => retry;
    }
}

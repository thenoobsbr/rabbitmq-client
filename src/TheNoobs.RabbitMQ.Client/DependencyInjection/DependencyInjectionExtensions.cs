using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Abstractions.Attributes;
using TheNoobs.RabbitMQ.Client.Abstractions;
using TheNoobs.RabbitMQ.Client.OpenTelemetry;

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
        if (amqpBuilder.TelemetrySource is not null)
        {
            services.AddSingleton(new OpenTelemetryPropagator(amqpBuilder.TelemetrySource));
        }
        services.AddHostedService<AmqpConsumerWorker>();
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
            var queueAttribute = handler.GetCustomAttribute<AmqpQueueAttribute>();
            
            if (queueAttribute == null)
            {
                throw new InvalidOperationException($"AmqpQueueAttribute not found in {handler.Name}");
            }
            
            var retryAttribute = handler.GetCustomAttribute<AmqpRetryAttribute>();
            var queueBindings = handler
                .GetCustomAttributes<AmqpQueueBindingAttribute>()
                .Cast<IAmqpQueueBinding>()
                .ToArray();
            services.AddSingleton(typeof(IAmqpConsumerConfiguration),
                new AmqpConsumerConfiguration(
                    handler,
                    retryAttribute,
                    queueAttribute.QueueName,
                    queueBindings));
        }

        return services;
    }

    class AmqpConfigurationBuilder : IAmqpConfigurationBuilder
    {
        internal string ConnectionString { get; private set; } = string.Empty;
        internal Type SerializerType { get; private set; } = typeof(AmqpDefaultJsonSerializer);
        internal List<Assembly> Assemblies { get; } = new();
        internal ActivitySource? TelemetrySource { get; private set; }

        public IAmqpConfigurationBuilder UseConnectionString(string connectionString)
        {
            ConnectionString = connectionString;
            return this;
        }

        public IAmqpConfigurationBuilder UseSerializer<TSerializer>() where TSerializer : IAmqpSerializer
        {
            SerializerType = typeof(TSerializer);
            return this;
        }

        public IAmqpConfigurationBuilder AddConsumersFromAssemblies(params Assembly[] assemblies)
        {
            Assemblies.AddRange(assemblies);
            return this;
        }

        public IAmqpConfigurationBuilder UseOpenTelemetry(ActivitySource activitySource)
        {
            TelemetrySource = activitySource;
            return this;
        }
    }
    
    class AmqpConsumerConfiguration(Type handlerType, IAmqpRetry? retry, AmqpQueueName queueName, IAmqpQueueBinding[] bindings) : IAmqpConsumerConfiguration
    {
        public Type HandlerType => handlerType;
        public Type RequestType => handlerType.GetInterface(typeof(IAmqpConsumer<>).Name)!.GetGenericArguments().First();
        public AmqpQueueName QueueName => queueName;
        public IAmqpRetry? Retry => retry;
        public IAmqpQueueBinding[] Bindings => bindings;
    }
}

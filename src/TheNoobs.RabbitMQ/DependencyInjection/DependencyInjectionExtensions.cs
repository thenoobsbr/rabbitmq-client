using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Abstractions.Attributes;

namespace TheNoobs.RabbitMQ.DependencyInjection;

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
        services.AddHostedService<AmqpConsumerWorker>();
        return services
            .AddConsumers(amqpBuilder)
            .AddPipelines(amqpBuilder);
    }
    
    private static IServiceCollection AddPipelines(this IServiceCollection services, AmqpConfigurationBuilder amqpBuilder)
    {
        var pipelineHandlers = amqpBuilder.PipelinesAssemblies
            .SelectMany(x => x.GetTypes())
            .Where(x =>
                x.GetInterface(typeof(IAmqpConsumerPipeline<,>).Name) != null && x is { IsClass: true, IsAbstract: false }
            )
            .ToList();
        foreach (var handlerType in pipelineHandlers)
        {
            services.AddScoped(typeof(IAmqpConsumerPipeline<,>), handlerType);
        }
        return services;
    }

    private static IServiceCollection AddConsumers(this IServiceCollection services, AmqpConfigurationBuilder amqpBuilder)
    {
        var consumerHandlers = amqpBuilder.ConsumersAssemblies
            .SelectMany(x => x.GetTypes())
            .Where(x =>
                x.GetInterface(typeof(IAmqpConsumer<,>).Name) != null && x is { IsClass: true, IsAbstract: false }
            )
            .ToList();
        foreach (var handlerType in consumerHandlers)
        {
            services.AddScoped(handlerType);

            var consumerType = handlerType.GetInterface(typeof(IAmqpConsumer<,>).Name)!;
            services.AddScoped(consumerType, provider =>
            {
                var pipelineType = typeof(IAmqpConsumerPipeline<,>).MakeGenericType(consumerType.GenericTypeArguments);
                var pipelines = provider.GetServices(pipelineType);
                var pipelineHandlerType =
                    typeof(AmqpConsumerPipelineHandler<,>).MakeGenericType(consumerType.GenericTypeArguments);
                var handler = provider.GetRequiredService(handlerType);
                return Activator.CreateInstance(pipelineHandlerType, handler, pipelines)
                       ?? throw new InvalidOperationException($"Unable to instantiate pipeline handler for {handlerType.Name}");
            });
            var queueAttribute = handlerType.GetCustomAttribute<AmqpQueueAttribute>();
            
            if (queueAttribute == null)
            {
                throw new InvalidOperationException($"AmqpQueueAttribute not found in {handlerType.Name}");
            }

            var retryAttribute = handlerType
                .GetCustomAttribute<AmqpRetryDelayAttribute>();
            var queueBindings = handlerType
                .GetCustomAttributes<AmqpQueueBindingAttribute>()
                .Cast<IAmqpQueueBinding>()
                .ToArray();
            services.AddSingleton(typeof(IAmqpConsumerConfiguration),
                new AmqpConsumerConfiguration(
                    handlerType,
                    retryAttribute?.Delay ?? amqpBuilder.DefaultRetryDelay,
                    queueAttribute.QueueName,
                    queueBindings));
        }

        return services;
    }

    class AmqpConfigurationBuilder : IAmqpConfigurationBuilder
    {
        internal string ConnectionString { get; private set; } = string.Empty;
        internal Type SerializerType { get; private set; } = typeof(AmqpDefaultJsonSerializer);
        internal List<Assembly> ConsumersAssemblies { get; } = new();
        internal List<Assembly> PipelinesAssemblies { get; } = new();
        internal TimeSpan DefaultRetryDelay { get; private set; } = TimeSpan.FromMinutes(1);

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
            ConsumersAssemblies.AddRange(assemblies);
            return this;
        }

        public IAmqpConfigurationBuilder AddPipelinesFromAssemblies(params Assembly[] assemblies)
        {
            PipelinesAssemblies.AddRange(assemblies);
            return this;
        }
        
        public IAmqpConfigurationBuilder AddConsumersAndPipelinesFromAssemblies(params Assembly[] assemblies)
        {
            AddConsumersAndPipelinesFromAssemblies(assemblies);
            AddPipelinesFromAssemblies(assemblies);
            return this;
        }
        
        public IAmqpConfigurationBuilder UseDefaultRetryDelay(TimeSpan delay)
        {
            if (delay.TotalSeconds < 1)
            {
                throw new ArgumentException("The retry delay must be greater than 1 seconds");
            }
            DefaultRetryDelay = delay;
            return this;
        }
    }
    
    class AmqpConsumerConfiguration(Type handlerType, TimeSpan retryDelay, AmqpQueueName queueName, IAmqpQueueBinding[] bindings) : IAmqpConsumerConfiguration
    {
        public Type HandlerType => handlerType;
        public AmqpQueueName QueueName => queueName;
        public TimeSpan RetryDelay => retryDelay;
        public IAmqpQueueBinding[] Bindings => bindings;
    }
}

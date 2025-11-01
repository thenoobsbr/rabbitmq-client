using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Testcontainers.RabbitMq;
using Testcontainers.Xunit;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Client.DependencyInjection;
using TheNoobs.RabbitMQ.Client.Tests.Stubs;
using Xunit.Abstractions;
using EventHandler = TheNoobs.RabbitMQ.Client.Tests.Stubs.EventHandler;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Client.Tests.DependencyInjection;

public class DependencyInjectionExtensionsTest(ITestOutputHelper output)
    : ContainerTest<RabbitMqBuilder, RabbitMqContainer>(output)
{
    [Fact]
    public void Should_Register_Services_Successfully()
    {
        var services = new ServiceCollection();
        services.AddRabbitMQClient(builder => builder
            .AddConsumersFromAssemblies(typeof(DependencyInjectionExtensionsTest).Assembly)
            .UseConnectionString(Container.GetConnectionString()));
        
        services.Any(x => x.ServiceType == typeof(IAmqpPublisher)).ShouldBeTrue();
        services.Any(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType?.Name == "AmqpConsumerWorker")
            .ShouldBeTrue();
        
        services.Any(x => x.ServiceType == typeof(EventHandler)).ShouldBeTrue();
        services.Any(x => x.ServiceType == typeof(IAmqpConsumer<StubMessage, Void>)).ShouldBeTrue();
    }
    
    [Fact]
    public void Should_Decorate_Consumers_With_Pipelines()
    {
        var services = new ServiceCollection();
        services.AddRabbitMQClient(builder => builder
            .AddConsumersFromAssemblies(typeof(DependencyInjectionExtensionsTest).Assembly)
            .UseConnectionString(Container.GetConnectionString()));
        var provider = services.BuildServiceProvider();
        
        var consumer = provider.GetRequiredService<IAmqpConsumer<StubMessage, Void>>();
        consumer.GetType().Name.ShouldBe("AmqpConsumerPipelineHandler`2");
    }
    
    [Fact]
    public async Task Should_Execute_Pipeline_Successfully()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IAmqpConsumerPipeline<,>), typeof(PipelineHandler<,>));
        services.AddRabbitMQClient(builder => builder
            .AddConsumersFromAssemblies(typeof(DependencyInjectionExtensionsTest).Assembly)
            .UseConnectionString(Container.GetConnectionString()));
        var provider = services.BuildServiceProvider();
        
        var message = Substitute.For<IAmqpMessage<StubMessage>>();

        var pipeline = (PipelineHandler<StubMessage, Void>) provider.GetRequiredService<IAmqpConsumerPipeline<StubMessage, Void>>();
        pipeline.Action = (amqpMessage, next, token) =>
        {
            amqpMessage.ShouldBe(message);
            next.ShouldNotBeNull();
        };
        var consumer = provider.GetRequiredService<IAmqpConsumer<StubMessage, Void>>();
        var result = await consumer.HandleAsync(message, CancellationToken.None);
        
        result.IsSuccess.ShouldBeTrue();
    }
}

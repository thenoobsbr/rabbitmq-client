using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using Testcontainers.RabbitMq;
using Testcontainers.Xunit;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Tests.Stubs;
using TheNoobs.Results;
using TheNoobs.Results.Extensions;
using TheNoobs.Results.Types;
using Xunit.Abstractions;
using EventHandler = TheNoobs.RabbitMQ.Tests.Stubs.EventHandler;
using Void = TheNoobs.Results.Types.Void;

namespace TheNoobs.RabbitMQ.Tests;

public class AmqpConsumerTests(ITestOutputHelper output)
    : ContainerTest<RabbitMqBuilder, RabbitMqContainer>(output)
{
    [Fact]
    public async Task Should_Successfully_Create_And_Start_Consumer()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        
        var configuration = Substitute.For<IAmqpConsumerConfiguration>();
        configuration.QueueName.Returns(AmqpQueueName.Create("test").Value);
        configuration.HandlerType.Returns(typeof(ITestMessage));
        configuration.RetryDelay.Returns(TimeSpan.FromSeconds(5));
        
        var handler = new EventHandler((_, _) => ValueTask.FromResult(new Result<Void>(Void.Value)));
        
        var consumerResult = await SetupConsumer(amqpConnectionFactory, serializer, configuration, handler);

        consumerResult.IsSuccess.ShouldBeTrue();
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queueDeclareOk = await channel.QueueDeclarePassiveAsync(configuration.QueueName.Value);
        queueDeclareOk.ConsumerCount.ShouldBe<uint>(1);
        queueDeclareOk.MessageCount.ShouldBe<uint>(0);
    }
    
    [Fact]
    public async Task Should_Handle_Message_When_Published_To_Queue()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        
        var configuration = Substitute.For<IAmqpConsumerConfiguration>();
        configuration.QueueName.Returns(AmqpQueueName.Create("test").Value);
        configuration.HandlerType.Returns(typeof(EventHandler));
        configuration.RetryDelay.Returns(TimeSpan.FromSeconds(5));
        
        var semaphore = new SemaphoreSlim(0);
        
        var handler = new EventHandler((message, cancellationToken) =>
        {
            message.Message.ShouldBe("Test message");
            cancellationToken.ShouldBeOfType<CancellationToken>();
            semaphore.Release();
            return ValueTask.FromResult(new Result<Void>(Void.Value));
        });
        
        var consumerResult = await SetupConsumer(amqpConnectionFactory, serializer, configuration, handler);

        consumerResult.IsSuccess.ShouldBeTrue();
        
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        
        var testMessage = new StubMessage()
        {
            Message = "Test message"
        };
        var message = serializer.Serialize(testMessage);
        await channel.BasicPublishAsync("", configuration.QueueName.Value, true, message.Value);

        await semaphore.WaitAsync(TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task Should_Acknowledge_Message_When_Processed_Successfully()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        
        var configuration = Substitute.For<IAmqpConsumerConfiguration>();
        configuration.QueueName.Returns(AmqpQueueName.Create("test").Value);
        configuration.HandlerType.Returns(typeof(EventHandler));
        configuration.RetryDelay.Returns(TimeSpan.FromSeconds(5));
        
        var semaphore = new SemaphoreSlim(0);
        
        var handler = new EventHandler((_, _) =>
        {
            semaphore.Release();
            return ValueTask.FromResult(new Result<Void>(Void.Value));
        });
        
        var consumerResult = await SetupConsumer(amqpConnectionFactory, serializer, configuration, handler);

        consumerResult.IsSuccess.ShouldBeTrue();
        
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        
        var testMessage = new StubMessage()
        {
            Message = "Test message"
        };
        var message = serializer.Serialize(testMessage);
        await channel.BasicPublishAsync("", configuration.QueueName.Value, true, message.Value);

        await semaphore.WaitAsync(TimeSpan.FromSeconds(1));

        var messageCount = await channel.MessageCountAsync(configuration.QueueName.Value);
        messageCount.ShouldBe<uint>(0);
    }
    
    [Fact]
    public async Task Should_Send_Message_To_Dead_Letter_Queue_When_Handler_Fails_With_NoRetryFail()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        
        var configuration = Substitute.For<IAmqpConsumerConfiguration>();
        configuration.QueueName.Returns(AmqpQueueName.Create("test").Value);
        configuration.HandlerType.Returns(typeof(EventHandler));
        configuration.RetryDelay.Returns(TimeSpan.FromSeconds(5));
        
        var semaphore = new SemaphoreSlim(0);
        
        var handler = new EventHandler((_, _) =>
        {
            semaphore.Release();
            return ValueTask.FromResult(new Result<Void>(new NoRetryFail(new ServerErrorFail())));
        });
        
        var consumerResult = await SetupConsumer(amqpConnectionFactory, serializer, configuration, handler);

        consumerResult.IsSuccess.ShouldBeTrue();
        
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        
        var testMessage = new StubMessage()
        {
            Message = "Test message"
        };
        var message = serializer.Serialize(testMessage);
        await channel.BasicPublishAsync("", configuration.QueueName.Value, true, message.Value);

        await semaphore.WaitAsync(TimeSpan.FromSeconds(1));

        var messageCount = await channel.MessageCountAsync(configuration.QueueName.Value);
        messageCount.ShouldBe<uint>(0);
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        var deadLetterQueue = await channel.QueueDeclarePassiveAsync(configuration.QueueName.DeadLetterQueueName().Value);
        deadLetterQueue.MessageCount.ShouldBe<uint>(1);
    }
    
    [Fact]
    public async Task Should_Retry_Message_When_Handler_Fails()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        
        var configuration = Substitute.For<IAmqpConsumerConfiguration>();
        configuration.QueueName.Returns(AmqpQueueName.Create("test").Value);
        configuration.HandlerType.Returns(typeof(EventHandler));
        configuration.RetryDelay.Returns(TimeSpan.FromSeconds(5));
        
        var semaphore = new SemaphoreSlim(0);
        
        var handler = new EventHandler((_, _) =>
        {
            semaphore.Release();
            return ValueTask.FromResult(new Result<Void>(new ServerErrorFail()));
        });
        
        var consumerResult = await SetupConsumer(amqpConnectionFactory, serializer, configuration, handler);

        consumerResult.IsSuccess.ShouldBeTrue();
        
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        
        var testMessage = new StubMessage()
        {
            Message = "Test message"
        };
        var message = serializer.Serialize(testMessage);
        await channel.BasicPublishAsync("", configuration.QueueName.Value, true, message.Value);

        await semaphore.WaitAsync(TimeSpan.FromSeconds(1));

        var messageCount = await channel.MessageCountAsync(configuration.QueueName.Value);
        messageCount.ShouldBe<uint>(0);
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        var retryQueue = await channel.QueueDeclarePassiveAsync(configuration.QueueName.ScheduledQueueName(TimeSpan.FromSeconds(5)).Value);
        retryQueue.MessageCount.ShouldBe<uint>(1);
    }

    private async Task<Result<AmqpConsumer>> SetupConsumer<TResponse>(
        IAmqpConnectionFactory amqpConnectionFactory,
        IAmqpSerializer serializer,
        IAmqpConsumerConfiguration configuration,
        StubHandler<TResponse> handler)
        where TResponse : notnull
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(StubHandler<TResponse>)).Returns(handler);
        serviceProvider.GetService(typeof(IAmqpConsumer<StubMessage, TResponse>)).Returns(handler);
        
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(scope);
        
        var consumerResult = await AmqpConsumer.CreateAsync(
                amqpConnectionFactory,
                serializer,
                configuration,
                serviceScopeFactory, 
                CancellationToken.None)
            .BindAsync(x => x.Value.StartAsync(CancellationToken.None));
        
        return consumerResult.GetValue<AmqpConsumer>();
    }
}

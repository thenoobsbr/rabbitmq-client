using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;
using Testcontainers.RabbitMq;
using Testcontainers.Xunit;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Tests.Stubs;
using TheNoobs.Results;
using Xunit.Abstractions;

namespace TheNoobs.RabbitMQ.Tests;

public class AmqpPublisherTests(ITestOutputHelper output)
    : ContainerTest<RabbitMqBuilder, RabbitMqContainer>(output)
{
    [Fact]
    public async Task Should_Publish_Message_To_Queue_Successfully()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        var randomQueue = Guid.NewGuid().ToString();

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(randomQueue, false, false, false);
        
        var publisher = new AmqpPublisher(amqpConnectionFactory, serializer);
        var result = await publisher.PublishAsync(
            AmqpExchangeName.Direct,
            randomQueue,
            new StubMessage()
            {
                Message = "Test message"
            },
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        
        var messageCount = await channel.MessageCountAsync(randomQueue);
        var message = await channel.BasicGetAsync(randomQueue, true);
        var messageContentResult = serializer.Deserialize(typeof(StubMessage), message!.Body.Span);
        
        messageCount.ShouldBe<uint>(1);
        message.ShouldNotBeNull();
        messageContentResult.IsSuccess.ShouldBeTrue();
        messageContentResult.GetValue<StubMessage>().Value.Message.ShouldBe("Test message");
    }
    
    [Fact]
    public async Task Should_Declare_Exchange_Successfully()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        var randomQueue = Guid.NewGuid().ToString();

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        
        var publisher = new AmqpPublisher(amqpConnectionFactory, serializer);
        await publisher.PublishAsync(
            "test",
            randomQueue,
            new StubMessage()
            {
                Message = "Test message"
            },
            CancellationToken.None);
        
        await channel.ExchangeDeclarePassiveAsync("test")
            .ShouldNotThrowAsync();
    }
    
    [Fact]
    public async Task Should_Send_Message_As_Rpc_Successfully()
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(Container.GetConnectionString())
        };
        var amqpConnectionFactory = new AmqpConnectionFactory(connectionFactory);
        var serializer = new AmqpDefaultJsonSerializer(new JsonSerializerOptions());
        var randomQueue = Guid.NewGuid().ToString();

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(randomQueue, false, false, false);
        
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, deliverEventArgs) =>
        {
            var message = (StubMessage)serializer.Deserialize(typeof(StubMessage), deliverEventArgs.Body.Span);
            message.Message += " - Received";
            
            var rpcResponse = RpcResponse.Create(new Result<StubMessage>(message), serializer);

            var properties = new BasicProperties();
            properties.CorrelationId = deliverEventArgs.BasicProperties.CorrelationId;
            await channel.BasicPublishAsync("", deliverEventArgs.BasicProperties.ReplyTo!,
                serializer.Serialize(rpcResponse.Value).Value);
        };
        await channel.BasicConsumeAsync(randomQueue, true, consumer);
        
        var publisher = new AmqpPublisher(amqpConnectionFactory, serializer);
        var result = await publisher.SendAsync<StubMessage, StubMessage>(
            AmqpExchangeName.Direct,
            randomQueue,
            new StubMessage()
            {
                Message = "Test message"
            },
            TimeSpan.FromSeconds(180),
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        
        result.Value.Message.ShouldBe("Test message - Received");
    }
}

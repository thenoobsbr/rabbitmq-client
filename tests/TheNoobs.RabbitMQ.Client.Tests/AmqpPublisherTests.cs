using System.Text.Json;
using RabbitMQ.Client;
using Shouldly;
using Testcontainers.RabbitMq;
using Testcontainers.Xunit;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.Results.Extensions;
using Xunit.Abstractions;

namespace TheNoobs.RabbitMQ.Client.Tests;

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
        var result = await AmqpMessage<TestMessage>.Create(new TestMessage()
            {
                Message = "Test message"
            }).BindAsync(x => publisher.PublishAsync(
            "",
            randomQueue,
            x.Value,
            CancellationToken.None));
        var messageCount = await channel.MessageCountAsync(randomQueue);
        var message = await channel.BasicGetAsync(randomQueue, true);
        var messageContentResult = serializer.Deserialize(typeof(TestMessage), message!.Body.Span);
        
        result.IsSuccess.ShouldBeTrue();
        messageCount.ShouldBe<uint>(1);
        message.ShouldNotBeNull();
        messageContentResult.IsSuccess.ShouldBeTrue();
        messageContentResult.GetValue<TestMessage>().Value.Message.ShouldBe("Test message");
    }

    class TestMessage
    {
        public string Message { get; set; } = string.Empty;
    }
}

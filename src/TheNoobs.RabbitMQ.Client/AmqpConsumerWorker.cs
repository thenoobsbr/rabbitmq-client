using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Client.Abstractions;
using TheNoobs.Results.Extensions;

namespace TheNoobs.RabbitMQ.Client;

public class AmqpConsumerWorker: BackgroundService
{
    private readonly IAmqpConnectionFactory _connectionFactory;
    private readonly IEnumerable<IAmqpConsumerConfiguration> _consumerConfigurations;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IAmqpSerializer _serializer;

    public AmqpConsumerWorker(
        IAmqpConnectionFactory connectionFactory,
        IEnumerable<IAmqpConsumerConfiguration> consumerConfigurations,
        IServiceScopeFactory serviceScopeFactory,
        IAmqpSerializer serializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _consumerConfigurations = consumerConfigurations ?? throw new ArgumentNullException(nameof(consumerConfigurations));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumers = await _consumerConfigurations
            .Select(x =>
                AmqpConsumer.CreateAsync(
                    _connectionFactory,
                    _serializer,
                    x,
                    _serviceScopeFactory,
                    stoppingToken)
                    .BindAsync(xy => xy.Value.StartAsync(stoppingToken)))
            .MergeAsync();

        while (!stoppingToken.WaitHandle.WaitOne(TimeSpan.FromMinutes(10)))
        {
            // TODO: Add logging
        }

        foreach (var consumerResult in consumers.Value)
        {
            await consumerResult.GetValue<AmqpConsumer>()
                .Value.DisposeAsync();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheNoobs.RabbitMQ.Abstractions;
using TheNoobs.RabbitMQ.Client.Abstractions;
using TheNoobs.Results.Extensions;

namespace TheNoobs.RabbitMQ.Client;

internal class AmqpConsumerWorker: IHostedService
{
    private readonly ILogger<AmqpConsumerWorker> _logger;
    private readonly IAmqpConnectionFactory _connectionFactory;
    private readonly IEnumerable<IAmqpConsumerConfiguration> _consumerConfigurations;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IAmqpSerializer _serializer;
    private AmqpConsumer[] _consumers;

    public AmqpConsumerWorker(
        ILogger<AmqpConsumerWorker> logger,
        IAmqpConnectionFactory connectionFactory,
        IEnumerable<IAmqpConsumerConfiguration> consumerConfigurations,
        IServiceScopeFactory serviceScopeFactory,
        IAmqpSerializer serializer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _consumerConfigurations = consumerConfigurations ?? throw new ArgumentNullException(nameof(consumerConfigurations));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _consumers = [];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_consumerConfigurations.Any())
        {
            return;
        }
        
        do
        {
            var consumers = await _consumerConfigurations
                .Select(x =>
                    AmqpConsumer.CreateAsync(
                            _connectionFactory,
                            _serializer,
                            x,
                            _serviceScopeFactory,
                            cancellationToken)
                        .BindAsync(xy => xy.Value.StartAsync(cancellationToken)))
                .MergeAsync();

            if (consumers.IsSuccess)
            {
                _consumers = consumers
                    .Value
                    .Select(x => x.GetValue<AmqpConsumer>().Value)
                    .ToArray();
                return;
            }
            
            _logger.LogError(consumers.Fail.Exception, "Failed to start consumers for @{configurations}", _consumerConfigurations);
        } while (!cancellationToken.IsCancellationRequested);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var consumer in _consumers)
        {
            await consumer.DisposeAsync();
        }
    }
}

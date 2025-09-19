using Microsoft.Extensions.Hosting;

namespace TheNoobs.RabbitMQ.Client;

public class ConsumerWorker: BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}

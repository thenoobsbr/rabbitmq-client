# TheNoobs.RabbitMQ.Client

[![SonarCloud](https://sonarcloud.io/api/project_badges/measure?project=thenoobsbr_rabbitmq-client&metric=alert_status)](https://sonarcloud.io/dashboard?id=YOUR_SONARCLOUD_PROJECT_KEY)
[![NuGet](https://img.shields.io/nuget/v/TheNoobs.RabbitMQ.Client.svg)](https://www.nuget.org/packages/TheNoobs.RabbitMQ.Client)

## Overview

**TheNoobs.RabbitMQ.Client** is a modern .NET library that simplifies integration with RabbitMQ. It provides abstractions and utilities for building robust, scalable, and testable messaging solutions in .NET applications.

## Features
- Easy RabbitMQ integration for .NET 9.0+
- Publisher and consumer abstractions
- Attribute-based queue and retry configuration
- Dependency injection support
- Pluggable serialization
- Built-in retry and error handling
- Unit-testable interfaces

## Installation

Install via NuGet:

```shell
Install-Package TheNoobs.RabbitMQ.Client
```

Or with .NET CLI:

```shell
dotnet add package TheNoobs.RabbitMQ.Client
```

## Usage Example

```csharp
using TheNoobs.RabbitMQ.Client;

// Configure services in Startup.cs or Program.cs
services.AddRabbitMq(config =>
{
    config.WithConnection("amqp://user:password@localhost:5672/vhost");
    // Additional configuration
});

// Implement a consumer
public class MyConsumer : IAmqpConsumer<MyMessage>
{
    public Task ConsumeAsync(MyMessage message, CancellationToken cancellationToken)
    {
        // Handle message
        return Task.CompletedTask;
    }
}
```

## Documentation
- [API Reference](src/TheNoobs.RabbitMQ.Client)
- [Abstractions](src/TheNoobs.RabbitMQ.Abstractions)
- [Examples](tests/TheNoobs.RabbitMQ.Client.Tests)

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---
> ♥ Made with love!
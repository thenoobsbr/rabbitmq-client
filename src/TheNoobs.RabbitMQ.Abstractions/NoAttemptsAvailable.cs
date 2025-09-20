using TheNoobs.Results.Abstractions;

namespace TheNoobs.RabbitMQ.Abstractions;

public record NoAttemptsAvailable() : Fail("No attempts available", "no_attempts_available", null);

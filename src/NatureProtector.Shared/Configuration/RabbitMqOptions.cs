namespace NatureProtector.Shared.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "np";
    public string Password { get; init; } = "np_dev_pass";
    public string ExchangeName { get; init; } = "np.events";
}
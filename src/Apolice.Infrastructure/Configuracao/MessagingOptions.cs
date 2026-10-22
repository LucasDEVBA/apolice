using Microsoft.Extensions.Options;

namespace Apolice.Infrastructure.Configuracao;

public enum TransporteMensageria
{
    RabbitMq,
    Sqs,
    InMemory,
}

public sealed class MessagingOptions
{
    public const string Secao = "Messaging";

    public TransporteMensageria Transport { get; set; } = TransporteMensageria.RabbitMq;

    public RabbitMqOptions RabbitMq { get; } = new();

    public SqsOptions Sqs { get; } = new();
}

public sealed class RabbitMqOptions
{
    private const int PortaPadrao = 5672;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = PortaPadrao;

    // Vazios = credenciais padrão do cliente (guest), que o RabbitMQ só aceita a partir do localhost.
    public string? Username { get; set; }

    public string? Password { get; set; }

    public string VirtualHost { get; set; } = "/";
}

public sealed class SqsOptions
{
    public string? QueueUrl { get; set; }
}

internal sealed class ValidacaoMessagingOptions : IValidateOptions<MessagingOptions>
{
    private const int PortaMaxima = 65_535;

    public ValidateOptionsResult Validate(string? name, MessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Transport switch
        {
            TransporteMensageria.RabbitMq when string.IsNullOrWhiteSpace(options.RabbitMq.Host) =>
                ValidateOptionsResult.Fail("Messaging:RabbitMq:Host é obrigatório com Messaging:Transport=RabbitMq."),
            TransporteMensageria.RabbitMq when options.RabbitMq.Port is < 1 or > PortaMaxima =>
                ValidateOptionsResult.Fail("Messaging:RabbitMq:Port inválida."),
            TransporteMensageria.Sqs when !Uri.TryCreate(options.Sqs.QueueUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps =>
                ValidateOptionsResult.Fail("Messaging:Sqs:QueueUrl deve ser a URL https da fila com Messaging:Transport=Sqs."),
            _ => ValidateOptionsResult.Success,
        };
    }
}

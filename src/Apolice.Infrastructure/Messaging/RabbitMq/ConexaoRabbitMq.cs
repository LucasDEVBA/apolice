using Apolice.Infrastructure.Configuracao;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Apolice.Infrastructure.Messaging.RabbitMq;

// Uma conexão por processo (o RabbitMQ recomenda poucas conexões e muitos canais), aberta sob demanda e com
// recuperação automática de conexão e de topologia (filas, bindings e consumidores voltam sozinhos após queda).
public sealed class ConexaoRabbitMq(IOptions<MessagingOptions> opcoes) : IAsyncDisposable
{
    private static readonly TimeSpan IntervaloRecuperacao = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IConnection? _conexao;

    public async Task<IConnection> ObterAsync(CancellationToken cancellationToken)
    {
        if (_conexao is { } existente)
        {
            return existente;
        }

        await _trava.WaitAsync(cancellationToken);
        try
        {
            return _conexao ??= await CriarFabrica(opcoes.Value.RabbitMq).CreateConnectionAsync("apolice", cancellationToken);
        }
        finally
        {
            _trava.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexao is { } conexao)
        {
            await conexao.CloseAsync();
            await conexao.DisposeAsync();
        }

        _trava.Dispose();
    }

    private static ConnectionFactory CriarFabrica(RabbitMqOptions opcoes)
    {
        var fabrica = new ConnectionFactory
        {
            HostName = opcoes.Host,
            Port = opcoes.Port,
            VirtualHost = opcoes.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = IntervaloRecuperacao,
        };

        if (!string.IsNullOrEmpty(opcoes.Username))
        {
            fabrica.UserName = opcoes.Username;
        }

        if (!string.IsNullOrEmpty(opcoes.Password))
        {
            fabrica.Password = opcoes.Password;
        }

        return fabrica;
    }
}

using System.Collections.Concurrent;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices;
using Apolice.Domain.Produtos;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Tests.Suporte;

internal sealed class FakeUsuarioAtual : ICurrentUser
{
    public Guid? UsuarioId { get; private set; }

    public string? Nome { get; private set; }

    public Papel? Papel { get; private set; }

    public bool Autenticado => UsuarioId is not null;

    // Permite simular uma implementação da infra que calcule PodeVerTudo por conta própria (e errado).
    public bool? PodeVerTudoForcado { get; set; }

    public bool PodeVerTudo => PodeVerTudoForcado ?? Papel is Domain.Usuarios.Papel.Analista or Domain.Usuarios.Papel.Admin;

    public void Definir(Usuario usuario)
    {
        UsuarioId = usuario.Id;
        Nome = usuario.Nome;
        Papel = usuario.Papel;
    }

    public void Anonimo()
    {
        UsuarioId = null;
        Nome = null;
        Papel = null;
    }
}

internal sealed class FakeFeatureFlags : IFeatureFlags
{
    private readonly HashSet<string> _desligadas = new(StringComparer.Ordinal);

    public void Desligar(string flag) => _desligadas.Add(flag);

    public Task<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken) => Task.FromResult(!_desligadas.Contains(flag));
}

internal sealed record RegistroAuditoria(string Acao, string Entidade, string? EntidadeId, string? Detalhes);

// Como a Auditoria da infra: só enfileira no mesmo "DbContext" (a sessão do escopo) e grava junto com o SaveChanges.
internal sealed class FakeAuditoria(SessaoBanco sessao) : IAuditoria
{
    public void Registrar(string acao, string entidade, string? entidadeId = null, string? detalhes = null) =>
        sessao.RegistrarAuditoria(new RegistroAuditoria(acao, entidade, entidadeId, detalhes));
}

// O que de fato foi gravado na tabela de auditoria.
internal sealed class AuditoriaGravada(BancoEmMemoria banco)
{
    public IReadOnlyList<RegistroAuditoria> Registros => banco.Auditoria;

    public IEnumerable<string> Acoes => banco.Auditoria.Select(r => r.Acao);
}

// Como o HybridCache da infra: guarda um retrato próprio do catálogo, só recarregado depois de InvalidarAsync, e
// entrega cópias exclusivas a cada chamada. Assim, ler pelo catálogo em vez do repositório, ou invalidar antes do
// commit, deixa a taxa velha visível — exatamente o defeito que os testes precisam enxergar.
internal sealed class FakeCatalogo(BancoEmMemoria banco) : ICatalogoProdutos
{
    private List<Produto>? _retrato;

    public List<int> SavesNaInvalidacao { get; } = [];

    public int Invalidacoes => SavesNaInvalidacao.Count;

    public bool FalharInvalidacao { get; set; }

    public Task<IReadOnlyList<Produto>> ListarAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Produto>>([.. Retrato().Select(Estado.Clonar)]);

    public Task<Produto?> ObterAsync(string codigo, CancellationToken cancellationToken) =>
        Task.FromResult(Retrato().Find(p => p.Codigo == codigo) is { } produto ? Estado.Clonar(produto) : null);

    public Task InvalidarAsync(CancellationToken cancellationToken)
    {
        if (FalharInvalidacao)
        {
            throw new InvalidOperationException("Cache distribuído indisponível.");
        }

        _retrato = null;
        SavesNaInvalidacao.Add(banco.TotalSaves);
        return Task.CompletedTask;
    }

    // Carrega o retrato agora (como a primeira leitura que popula o cache).
    public void Aquecer() => Retrato();

    private List<Produto> Retrato() => _retrato ??= [.. banco.Produtos.Select(Estado.Clonar)];
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Prefixo = "hash:";

    public int Verificacoes { get; private set; }

    public int VerificacoesSimuladas { get; private set; }

    public string Hash(string senha) => Prefixo + senha;

    public bool Verificar(string senhaHash, string senha)
    {
        Verificacoes++;
        return senhaHash == Prefixo + senha;
    }

    public void SimularVerificacao(string senha) => VerificacoesSimuladas++;
}

internal sealed class FakeTokenService(TimeProvider tempo) : ITokenService
{
    private int _emitidos;

    public TimeSpan ValidadeRefreshToken { get; } = TimeSpan.FromDays(7);

    public (string Token, DateTimeOffset ExpiraEm) GerarAccessToken(Usuario usuario) =>
        ($"access:{usuario.Id}:{Interlocked.Increment(ref _emitidos)}", tempo.GetUtcNow().AddMinutes(15));

    public string GerarRefreshToken() => $"refresh-{Guid.NewGuid():N}";
}

internal sealed class FakeGeradorDocumento : IGeradorDocumentoApolice
{
    public ApoliceDto? UltimaApolice { get; private set; }

    public Task<byte[]> GerarAsync(ApoliceDto apolice, CancellationToken cancellationToken)
    {
        UltimaApolice = apolice;
        return Task.FromResult<byte[]>([0x25, 0x50, 0x44, 0x46]);
    }
}

internal sealed class ColetorLogs
{
    public ConcurrentQueue<string> Mensagens { get; } = new();
}

// Logger sempre habilitado: executa as mensagens geradas e permite verificar que nenhuma vaza dado sensível.
internal sealed class LoggerDeTeste<T>(ColetorLogs coletor) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var propriedades = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
        coletor.Mensagens.Enqueue($"{logLevel}: {formatter(state, exception)} | {string.Join(';', propriedades.Select(p => $"{p.Key}={p.Value}"))}");
    }
}

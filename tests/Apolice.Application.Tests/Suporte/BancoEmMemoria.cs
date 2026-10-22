using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Suporte;

// O "banco" em si: só estado CONFIRMADO. Os handlers nunca tocam estas instâncias — leem cópias por uma SessaoBanco
// (um DbContext por escopo de DI), e só um SaveChanges bem-sucedido copia o estado de volta. Assim, uma mutação sem
// SaveChanges, ou de uma operação que falhou, nunca aparece aqui nem nas consultas (que, como o Dapper, leem daqui).
// Os testes podem arranjar cenários alterando estas instâncias diretamente (o equivalente a um UPDATE manual).
internal sealed class BancoEmMemoria : INumerador
{
    private readonly List<SessaoBanco> _sessoes = [];
    private Action? _escritaConcorrente;
    private int _conflitosForcados;

    public List<Cotacao> Cotacoes { get; } = [];

    public List<ApoliceSeguro> Apolices { get; } = [];

    public List<Sinistro> Sinistros { get; } = [];

    public List<Produto> Produtos { get; } = [.. CatalogoPadrao.Produtos()];

    public List<Usuario> Usuarios { get; } = [];

    public List<RefreshToken> RefreshTokens { get; } = [];

    public List<Notificacao> Notificacoes { get; } = [];

    public HashSet<(Guid MensagemId, string Consumidor)> MensagensProcessadas { get; } = [];

    public List<RegistroAuditoria> Auditoria { get; } = [];

    // O numerador roda fora da transação do caso de uso (INSERT ... ON CONFLICT em autocommit): confirma na hora.
    public Dictionary<string, long> Numeradores { get; } = new(StringComparer.Ordinal);

    public List<IDomainEvent> Outbox { get; } = [];

    public int TotalSaves { get; private set; }

    public int TotalConflitos { get; private set; }

    // Alterações rastreadas ou enfileiradas em alguma sessão e ainda não gravadas.
    public int PendentesNaoSalvos => _sessoes.Sum(s => s.AlteracoesPendentes);

    public bool FalharProximoSaveComConflito
    {
        get => _conflitosForcados > 0;
        set => _conflitosForcados = value ? 1 : 0;
    }

    public void ForcarConflitos(int quantidade) => _conflitosForcados = quantidade;

    // Outra transação que grava entre a leitura e o SaveChanges da sessão: roda uma vez, no início do próximo save,
    // e incrementa a versão do agregado alterado — o save da sessão então conflita como no banco real.
    public void AgendarEscritaConcorrente<T>(T confirmado, Action<T> alteracao)
        where T : class
    {
        _escritaConcorrente = () =>
        {
            alteracao(confirmado);
            Estado.DefinirVersao(confirmado, Estado.Versao(confirmado) + 1);
        };
    }

    public void AdicionarConfirmado(Usuario usuario) => Usuarios.Add(usuario);

    Task<long> INumerador.ProximoAsync(string chave, CancellationToken cancellationToken)
    {
        var proximo = Numeradores.GetValueOrDefault(chave) + 1;
        Numeradores[chave] = proximo;
        return Task.FromResult(proximo);
    }

    internal void RegistrarSessao(SessaoBanco sessao) => _sessoes.Add(sessao);

    internal void AntesDeGravar()
    {
        var escrita = _escritaConcorrente;
        _escritaConcorrente = null;
        escrita?.Invoke();
        if (_conflitosForcados <= 0)
        {
            return;
        }

        _conflitosForcados--;
        LancarConflito();
    }

    internal void LancarConflito()
    {
        TotalConflitos++;
        throw new ConflitoDeConcorrenciaException();
    }

    internal bool Contem(object confirmado) => Lista(confirmado).Cast<object>().Any(e => ReferenceEquals(e, confirmado));

    // PK (mensagem_id, consumidor) de mensagens_processadas: duplicata derruba a transação inteira, sem gravar nada.
    internal void ValidarInbox(IReadOnlyCollection<(Guid MensagemId, string Consumidor)> pendentes)
    {
        if (pendentes.Distinct().Count() != pendentes.Count || pendentes.Any(MensagensProcessadas.Contains))
        {
            throw new InvalidOperationException("Violação da chave primária de mensagens_processadas.");
        }
    }

    internal object Inserir(object instancia)
    {
        var confirmado = Estado.Clonar(instancia);
        Lista(confirmado).Add(confirmado);
        return confirmado;
    }

    internal void Confirmar(IEnumerable<IDomainEvent> eventos, IEnumerable<(Guid, string)> inbox, IEnumerable<RegistroAuditoria> auditoria)
    {
        Outbox.AddRange(eventos);
        MensagensProcessadas.UnionWith(inbox);
        Auditoria.AddRange(auditoria);
        TotalSaves++;
    }

    private System.Collections.IList Lista(object entidade) => entidade switch
    {
        Cotacao => Cotacoes,
        ApoliceSeguro => Apolices,
        Sinistro => Sinistros,
        Produto => Produtos,
        Usuario => Usuarios,
        RefreshToken => RefreshTokens,
        Notificacao => Notificacoes,
        _ => throw new InvalidOperationException($"Tipo não suportado: {entidade.GetType().Name}."),
    };
}

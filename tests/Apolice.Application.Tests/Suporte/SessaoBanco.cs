using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Suporte;

// Simula o DbContext de um escopo de DI: leituras devolvem cópias rastreadas do estado gravado (identity map: a
// mesma instância para o mesmo id), Adicionar só enfileira, e SaveChanges grava tudo ou nada — com concorrência
// otimista pela Versao (como o VersaoInterceptor), recolhendo os eventos de domínio para o outbox (como o
// DomainEventsToOutboxInterceptor) e confirmando inbox e auditoria na mesma "transação".
internal sealed class SessaoBanco :
    ICotacaoRepository,
    IApoliceRepository,
    ISinistroRepository,
    IProdutoRepository,
    IUsuarioRepository,
    IRefreshTokenRepository,
    INotificacaoRepository,
    IUnitOfWork,
    IMensagensProcessadas
{
    private readonly BancoEmMemoria _banco;
    private readonly Dictionary<(Type Tipo, object Id), Entrada> _rastreadas = [];
    private readonly List<Entrada> _entradas = [];
    private readonly List<(Guid MensagemId, string Consumidor)> _inbox = [];
    private readonly List<RegistroAuditoria> _auditoria = [];

    public SessaoBanco(BancoEmMemoria banco)
    {
        _banco = banco;
        banco.RegistrarSessao(this);
    }

    public int AlteracoesPendentes => _entradas.Count(e => e.Alterada) + _inbox.Count + _auditoria.Count;

    public void RegistrarAuditoria(RegistroAuditoria registro) => _auditoria.Add(registro);

    Task<Cotacao?> ICotacaoRepository.ObterAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.Cotacoes.Find(c => c.Id == id)));

    void ICotacaoRepository.Adicionar(Cotacao cotacao) => Enfileirar(cotacao);

    Task<ApoliceSeguro?> IApoliceRepository.ObterAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.Apolices.Find(a => a.Id == id)));

    void IApoliceRepository.Adicionar(ApoliceSeguro apolice) => Enfileirar(apolice);

    Task<Sinistro?> ISinistroRepository.ObterAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.Sinistros.Find(s => s.Id == id)));

    void ISinistroRepository.Adicionar(Sinistro sinistro) => Enfileirar(sinistro);

    Task<Produto?> IProdutoRepository.ObterAsync(string codigo, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.Produtos.Find(p => p.Codigo == codigo)));

    Task<IReadOnlyList<Produto>> IProdutoRepository.ListarAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Produto>>([.. _banco.Produtos.Select(p => Rastrear(p)!)]);

    Task<Usuario?> IUsuarioRepository.ObterPorEmailAsync(Email email, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.Usuarios.Find(u => u.Email == email)));

    Task<Usuario?> IUsuarioRepository.ObterAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.Usuarios.Find(u => u.Id == id)));

    Task<RefreshToken?> IRefreshTokenRepository.ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Rastrear(_banco.RefreshTokens.Find(t => t.TokenHash == tokenHash)));

    // O filtro roda no estado gravado (é o WHERE do banco); a instância devolvida é a rastreada, se já houver.
    Task<IReadOnlyList<RefreshToken>> IRefreshTokenRepository.ListarNaoRevogadosDaFamiliaAsync(Guid familiaId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>([.. _banco.RefreshTokens.Where(t => t.FamiliaId == familiaId && t.RevogadoEm is null).Select(t => Rastrear(t)!)]);

    void IRefreshTokenRepository.Adicionar(RefreshToken token) => Enfileirar(token);

    void INotificacaoRepository.Adicionar(Notificacao notificacao) => Enfileirar(notificacao);

    Task<bool> IMensagensProcessadas.JaProcessadaAsync(Guid mensagemId, string consumidor, CancellationToken cancellationToken) =>
        Task.FromResult(_banco.MensagensProcessadas.Contains((mensagemId, consumidor)));

    void IMensagensProcessadas.Registrar(Guid mensagemId, string consumidor, DateTimeOffset agora) => _inbox.Add((mensagemId, consumidor));

    // Em conflito nada é gravado e a sessão fica como estava (o EF não limpa o ChangeTracker): refazer exige DescartarAlteracoes.
    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        _banco.AntesDeGravar();
        var alteradas = _entradas.Where(e => e.Confirmado is not null && e.Alterada).ToList();
        if (alteradas.Exists(e => !_banco.Contem(e.Confirmado!) || Estado.Versao(e.Instancia) != Estado.Versao(e.Confirmado!)))
        {
            _banco.LancarConflito();
        }

        _banco.ValidarInbox(_inbox);
        var novas = _entradas.Where(e => e.Confirmado is null).ToList();
        var total = alteradas.Count + novas.Count + _inbox.Count + _auditoria.Count;
        var eventos = RecolherEventos();
        alteradas.ForEach(GravarAlteracao);
        novas.ForEach(GravarInsercao);
        _banco.Confirmar(eventos, _inbox, _auditoria);
        _inbox.Clear();
        _auditoria.Clear();
        return Task.FromResult(total);
    }

    void IUnitOfWork.DescartarAlteracoes()
    {
        _rastreadas.Clear();
        _entradas.Clear();
        _inbox.Clear();
        _auditoria.Clear();
    }

    private static (Type Tipo, object Id) Chave(object entidade) => entidade switch
    {
        AggregateRoot<Guid> agregado => (entidade.GetType(), agregado.Id),
        AggregateRoot<string> agregado => (entidade.GetType(), agregado.Id),
        _ => throw new InvalidOperationException($"Tipo não suportado: {entidade.GetType().Name}."),
    };

    private T? Rastrear<T>(T? confirmado)
        where T : class
    {
        if (confirmado is null)
        {
            return null;
        }

        var chave = Chave(confirmado);
        if (_rastreadas.TryGetValue(chave, out var existente))
        {
            return (T)existente.Instancia;
        }

        var entrada = new Entrada(Estado.Clonar(confirmado), confirmado, Estado.Clonar(confirmado));
        _rastreadas[chave] = entrada;
        _entradas.Add(entrada);
        return (T)entrada.Instancia;
    }

    private void Enfileirar(object entidade) => _entradas.Add(new Entrada(entidade, null, null));

    private List<IDomainEvent> RecolherEventos()
    {
        var eventos = new List<IDomainEvent>();
        foreach (var instancia in _entradas.Select(e => e.Instancia))
        {
            if (instancia is AggregateRoot<Guid> agregado)
            {
                eventos.AddRange(agregado.DomainEvents);
                agregado.ClearDomainEvents();
            }
            else if (instancia is AggregateRoot<string> porCodigo)
            {
                eventos.AddRange(porCodigo.DomainEvents);
                porCodigo.ClearDomainEvents();
            }
        }

        return eventos;
    }

    private void GravarAlteracao(Entrada entrada)
    {
        var versao = Estado.Versao(entrada.Instancia) + 1;
        Estado.Copiar(entrada.Instancia, entrada.Confirmado!);
        Estado.DefinirVersao(entrada.Confirmado!, versao);
        Estado.DefinirVersao(entrada.Instancia, versao);
        entrada.Original = Estado.Clonar(entrada.Instancia);
    }

    // Depois de gravada, a entidade nova continua rastreada (como no EF), já ligada à cópia confirmada.
    private void GravarInsercao(Entrada entrada)
    {
        entrada.Confirmado = _banco.Inserir(entrada.Instancia);
        entrada.Original = Estado.Clonar(entrada.Instancia);
        _rastreadas[Chave(entrada.Instancia)] = entrada;
    }

    // Original = retrato do que foi lido (ou gravado) por esta sessão, como os valores originais do ChangeTracker:
    // "alterada" compara com ele, e não com o estado gravado atual, que outra sessão pode ter mudado no meio-tempo.
    private sealed class Entrada(object instancia, object? confirmado, object? original)
    {
        public object Instancia { get; } = instancia;

        public object? Confirmado { get; set; } = confirmado;

        public object? Original { get; set; } = original;

        public bool Alterada => Original is null || !Estado.Iguais(Instancia, Original);
    }
}

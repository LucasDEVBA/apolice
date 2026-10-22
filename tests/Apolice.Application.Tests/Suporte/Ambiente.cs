using System.Text.Json;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Criar;
using Apolice.Application.Features.Sinistros;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Apolice.Application.Tests.Suporte;

// Composição real (AddApplication) com fakes nas portas: o teste exercita a mesma DI que a API usará.
// Cada Servico<T>() abre um escopo novo — uma "requisição" com o seu próprio DbContext (SessaoBanco) —, então o que
// um caso de uso deixou sem gravar nunca vaza para o próximo.
internal sealed class Ambiente : IDisposable
{
    public const string SenhaPadrao = "Senha@Forte123";
    public const string DescricaoSinistro = "Colisão traseira em semáforo, para-choque e lanterna danificados.";

    // 10/03/2026 às 10:00 em Brasília.
    public static readonly DateTimeOffset Inicio = new(2026, 3, 10, 13, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provedor;
    private readonly List<IServiceScope> _escopos = [];

    public Ambiente()
    {
        Consultas = new ConsultasEmMemoria(Banco);
        Catalogo = new FakeCatalogo(Banco);
        Tokens = new FakeTokenService(Tempo);
        Auditoria = new AuditoriaGravada(Banco);
        Ana = CriarUsuario(PersonasDemo.Corretora);
        Bruno = CriarUsuario(PersonasDemo.Corretor);
        Carla = CriarUsuario(PersonasDemo.Analista);
        Diego = CriarUsuario(PersonasDemo.Admin);
        _provedor = MontarServicos().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Como(Ana);
    }

    public FakeTimeProvider Tempo { get; } = new(Inicio);

    public BancoEmMemoria Banco { get; } = new();

    public ConsultasEmMemoria Consultas { get; }

    public FakeUsuarioAtual UsuarioAtual { get; } = new();

    public FakeFeatureFlags Flags { get; } = new();

    public AuditoriaGravada Auditoria { get; }

    public FakeCatalogo Catalogo { get; }

    public FakeTokenService Tokens { get; }

    public FakePasswordHasher Hasher { get; } = new();

    public FakeGeradorDocumento Gerador { get; } = new();

    public IDashboardQueries Dashboard { get; } = Substitute.For<IDashboardQueries>();

    public IAuditoriaQueries ConsultasAuditoria { get; } = Substitute.For<IAuditoriaQueries>();

    public ColetorLogs Logs { get; } = new();

    public Usuario Ana { get; }

    public Usuario Bruno { get; }

    public Usuario Carla { get; }

    public Usuario Diego { get; }

    public DateTimeOffset Agora => Tempo.GetUtcNow();

    public DateOnly Hoje => Relogio.HojeEm(Agora);

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public T Servico<T>()
        where T : notnull
    {
        var escopo = _provedor.CreateScope();
        _escopos.Add(escopo);
        return escopo.ServiceProvider.GetRequiredService<T>();
    }

    public Ambiente Como(Usuario usuario)
    {
        UsuarioAtual.Definir(usuario);
        return this;
    }

    public void Avancar(TimeSpan intervalo) => Tempo.Advance(intervalo);

    public async Task<CotacaoDto> CriarCotacaoAsync(Usuario corretor, CotacaoRequest? request = null)
    {
        Como(corretor);
        var resultado = await Servico<CriarCotacaoHandler>().HandleAsync(new CriarCotacaoCommand(request ?? Dados.CotacaoAuto()), Ct);
        return resultado.DeveTerSucesso();
    }

    public async Task<CotacaoDto> CotacaoAceitaAsync(Usuario corretor, CotacaoRequest? request = null)
    {
        var cotacao = await CriarCotacaoAsync(corretor, request);
        var aceita = await Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);
        return aceita.DeveTerSucesso();
    }

    public async Task<ApoliceDto> EmitirApoliceAsync(Usuario corretor, int parcelas = 1, CotacaoRequest? request = null)
    {
        var cotacao = await CotacaoAceitaAsync(corretor, request);
        var emissao = await Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(cotacao.Id, Hoje, parcelas), Ct);
        return emissao.DeveTerSucesso();
    }

    public async Task<SinistroDto> AbrirSinistroAsync(Usuario corretor, Guid apoliceId, decimal valor = 8_000m, DateOnly? ocorrencia = null)
    {
        Como(corretor);
        var command = new AbrirSinistroCommand(apoliceId, CodigosCobertura.Casco, ocorrencia ?? Hoje, DescricaoSinistro, valor);
        var resultado = await Servico<AbrirSinistroHandler>().HandleAsync(command, Ct);
        return resultado.DeveTerSucesso();
    }

    // Faz o papel do relay do outbox + consumidor: serializa como a infra e entrega ao dispatcher, um escopo por mensagem.
    public async Task<int> ProcessarOutboxAsync()
    {
        var entregues = 0;
        while (Banco.Outbox.Count > 0)
        {
            var eventos = Banco.Outbox.ToList();
            Banco.Outbox.Clear();
            foreach (var evento in eventos)
            {
                await EntregarAsync(evento);
                entregues++;
            }
        }

        return entregues;
    }

    public Task EntregarAsync(IDomainEvent evento) =>
        Servico<IIntegrationEventDispatcher>().DispatchAsync(evento.EventoId, evento.GetType().Name, JsonSerializer.Serialize(evento, evento.GetType(), JsonOpcoes.Padrao), Ct);

    public void Dispose()
    {
        _escopos.ForEach(e => e.Dispose());
        _provedor.Dispose();
    }

    private Usuario CriarUsuario(string persona)
    {
        var demo = PersonasDemo.Usuarios.Single(u => u.Persona == persona);
        var usuario = Usuario.Criar(demo.Nome, Email.Criar(demo.Email).Value, Hasher.Hash(SenhaPadrao), demo.Papel, Inicio).Value;
        Banco.AdicionarConfirmado(usuario);
        return usuario;
    }

    private ServiceCollection MontarServicos()
    {
        var servicos = new ServiceCollection();
        servicos.AddSingleton<TimeProvider>(Tempo);
        servicos.AddApplication();
        servicos.AddSingleton(Logs);
        servicos.AddSingleton(typeof(ILogger<>), typeof(LoggerDeTeste<>));
        servicos.AddSingleton(Banco);
        servicos.AddSingleton<INumerador>(Banco);
        servicos.AddScoped<SessaoBanco>();
        Type[] portasDaSessao =
        [
            typeof(ICotacaoRepository), typeof(IApoliceRepository), typeof(ISinistroRepository), typeof(IProdutoRepository), typeof(IUsuarioRepository),
            typeof(IRefreshTokenRepository), typeof(INotificacaoRepository), typeof(IUnitOfWork), typeof(IMensagensProcessadas),
        ];
        foreach (var porta in portasDaSessao)
        {
            servicos.AddScoped(porta, sp => sp.GetRequiredService<SessaoBanco>());
        }

        servicos.AddScoped<IAuditoria, FakeAuditoria>();
        RegistrarComo(servicos, Consultas, typeof(ICotacaoQueries), typeof(IApoliceQueries), typeof(ISinistroQueries));
        servicos.AddSingleton(Dashboard);
        servicos.AddSingleton(ConsultasAuditoria);
        servicos.AddSingleton<ICurrentUser>(UsuarioAtual);
        servicos.AddSingleton<IFeatureFlags>(Flags);
        servicos.AddSingleton<ICatalogoProdutos>(Catalogo);
        servicos.AddSingleton<ITokenService>(Tokens);
        servicos.AddSingleton<IPasswordHasher>(Hasher);
        servicos.AddSingleton<IGeradorDocumentoApolice>(Gerador);
        return servicos;
    }

    private static void RegistrarComo(ServiceCollection servicos, object instancia, params Type[] contratos)
    {
        foreach (var contrato in contratos)
        {
            servicos.AddSingleton(contrato, instancia);
        }
    }
}

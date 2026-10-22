using Apolice.Application;
using Apolice.Application.Abstractions.Security;
using Apolice.Infrastructure;
using Apolice.Infrastructure.Persistence.Migracao;
using Apolice.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Apolice.Api.IntegrationTests.Infraestrutura.Suporte;

// Composição real (AddApplication + AddInfrastructure) contra um banco do container, com relógio controlado e usuário
// de teste no lugar do ICurrentUser da API. ValidateOnBuild: um registro de DI quebrado falha aqui, não em produção.
public sealed class AmbienteInfra : IAsyncDisposable
{
    public const string ChaveJwtTeste = "chave-de-teste-com-bem-mais-de-32-bytes-0123456789";
    public const string SenhaSeed = "senha-de-teste-123";

    private AmbienteInfra(ServiceProvider provedor, FakeTimeProvider tempo, UsuarioTeste usuario)
    {
        Provedor = provedor;
        Tempo = tempo;
        Usuario = usuario;
    }

    public ServiceProvider Provedor { get; }

    public FakeTimeProvider Tempo { get; }

    public UsuarioTeste Usuario { get; }

    public static async Task<AmbienteInfra> CriarAsync(
        string connectionString,
        IDictionary<string, string?>? configuracaoExtra = null,
        Action<IServiceCollection>? servicos = null,
        bool migrar = true,
        string ambiente = "Testing")
    {
        var valores = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["ConnectionStrings:Apolice"] = connectionString,
            ["Messaging:Transport"] = "InMemory",
            ["Outbox:DispatchImediato"] = "false",
            ["Jwt:SigningKey"] = ChaveJwtTeste,
            ["Seed:SenhaUsuarios"] = SenhaSeed,
        };
        foreach (var (chave, valor) in configuracaoExtra ?? new Dictionary<string, string?>())
        {
            valores[chave] = valor;
        }

        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var tempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.Zero));
        var usuario = new UsuarioTeste();
        var colecao = new ServiceCollection();
        colecao.AddLogging();
        colecao.AddSingleton<IConfiguration>(configuracao);
        colecao.AddSingleton<TimeProvider>(tempo);
        colecao.AddApplication();
        colecao.AddInfrastructure(configuracao, new AmbienteHost(ambiente));
        colecao.AddSingleton(usuario);
        colecao.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<UsuarioTeste>());
        servicos?.Invoke(colecao);

        var provedor = colecao.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var criado = new AmbienteInfra(provedor, tempo, usuario);
        if (migrar)
        {
            await criado.ExecutarAsync<IMigradorBanco>(m => m.MigrarAsync(TestContext.Current.CancellationToken));
        }

        return criado;
    }

    public async Task SemearBaseAsync() =>
        await ExecutarAsync<DataSeeder>(s => s.SemearAsync(TestContext.Current.CancellationToken));

    public async Task ExecutarAsync<T>(Func<T, Task> acao)
        where T : notnull
    {
        await using var escopo = Provedor.CreateAsyncScope();
        await acao(escopo.ServiceProvider.GetRequiredService<T>());
    }

    public async Task<TResultado> ExecutarAsync<T, TResultado>(Func<T, Task<TResultado>> acao)
        where T : notnull
    {
        await using var escopo = Provedor.CreateAsyncScope();
        return await acao(escopo.ServiceProvider.GetRequiredService<T>());
    }

    public async ValueTask DisposeAsync() => await Provedor.DisposeAsync();

    private sealed class AmbienteHost(string nome) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nome;

        public string ApplicationName { get; set; } = "Apolice.Api.IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

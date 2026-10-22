using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Persistence;
using Apolice.Infrastructure.Persistence.Migracao;
using Apolice.Infrastructure.Persistence.Seed;
using Apolice.Worker.Composicao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apolice.Api.IntegrationTests.Worker.Suporte;

// O host do Worker montado pela MESMA composição de produção (ComposicaoWorker), só com a configuração do teste no
// lugar de appsettings/env vars (DisableDefaults) e ValidateOnBuild ligado: um registro quebrado falha aqui.
internal static class HostWorker
{
    public const string SemBanco = "Host=localhost;Port=1;Database=nenhum;Username=ninguem";
    public const string SenhaSeed = "senha-de-teste-123";

    public static IHost Criar(string connectionString, IDictionary<string, string?>? configuracaoExtra = null, bool hostLocal = false)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["ConnectionStrings:Apolice"] = connectionString,
            ["Messaging:Transport"] = "InMemory",
            ["Outbox:DispatchImediato"] = "false",
            ["Seed:SenhaUsuarios"] = SenhaSeed,
            ["Seed:DadosDemo"] = "false",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        };
        foreach (var (chave, valor) in configuracaoExtra ?? new Dictionary<string, string?>())
        {
            valores[chave] = valor;
        }

        var configuracao = new ConfigurationManager();
        configuracao.AddInMemoryCollection(valores);
        var builder = ComposicaoWorker.CriarBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Testing",
            ApplicationName = "Apolice.Worker",
            ContentRootPath = AppContext.BaseDirectory,
            Configuration = configuracao,
        });
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
        if (hostLocal)
        {
            ComposicaoWorker.AdicionarHostLocal(builder);
        }

        return builder.Build();
    }

    // Os mesmos serviços dos verbos "migrate" e "seed" (catálogo + usuários demo, sem a carteira de demonstração).
    public static async Task PrepararBancoAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        await using (var escopo = servicos.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IMigradorBanco>().MigrarAsync(cancellationToken);
        }

        await using (var escopo = servicos.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<DataSeeder>().SemearAsync(cancellationToken);
        }
    }

    // Emite uma apólice pelos métodos do domínio; o evento ApoliceEmitida fica pendente no outbox (sem despacho imediato).
    public static async Task<Emissao> EmitirAsync(IServiceProvider servicos, CancellationToken cancellationToken, string? correlationId = null)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        sp.GetRequiredService<ContextoCorrelacao>().Definir(correlationId);
        var email = Email.Criar(PersonasDemo.EmailDe(PersonasDemo.Corretora)).Value;
        var ana = await sp.GetRequiredService<ApoliceDbContext>().Usuarios.AsNoTracking().SingleAsync(u => u.Email == email, cancellationToken);
        var cenarios = new Cenarios(sp);
        var apolice = await cenarios.ApoliceAutoAsync(ana.Id, DateTimeOffset.UtcNow);
        var eventoId = apolice.DomainEvents.OfType<ApoliceEmitida>().Single().EventoId;
        await cenarios.Uow.SaveChangesAsync(cancellationToken);
        return new Emissao(apolice.Id, eventoId);
    }

    public static async Task<T> ConsultarAsync<T>(IServiceProvider servicos, Func<ApoliceDbContext, Task<T>> consulta)
    {
        await using var escopo = servicos.CreateAsyncScope();
        return await consulta(escopo.ServiceProvider.GetRequiredService<ApoliceDbContext>());
    }

    public static async ValueTask DescartarAsync(IHost host)
    {
        if (host is IAsyncDisposable assincrono)
        {
            await assincrono.DisposeAsync();
            return;
        }

        host.Dispose();
    }
}

internal sealed record Emissao(Guid ApoliceId, Guid EventoId);

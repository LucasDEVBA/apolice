using Apolice.Application.Features.Auth;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Infraestrutura.Suporte;

// Banco novo por classe, migrado e com catálogo + usuários demo (sem a carteira de demonstração, salvo se pedida).
public abstract class TesteComBanco(PostgresFixture postgres, string prefixo) : IAsyncLifetime
{
    protected AmbienteInfra Ambiente { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected virtual bool DadosDemo => false;

    public async ValueTask InitializeAsync()
    {
        var configuracao = Configuracao();
        configuracao["Seed:DadosDemo"] = DadosDemo ? "true" : "false";
        Ambiente = await AmbienteInfra.CriarAsync(await postgres.CriarBancoAsync(prefixo), configuracao, Servicos);
        await Ambiente.SemearBaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Ambiente.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected virtual Dictionary<string, string?> Configuracao() => [];

    protected virtual void Servicos(IServiceCollection servicos)
    {
    }

    protected async Task<Usuario> UsuarioAsync(string persona)
    {
        var email = Email.Criar(PersonasDemo.EmailDe(persona)).Value;
        return await Ambiente.ExecutarAsync<ApoliceDbContext, Usuario>(db => db.Usuarios.AsNoTracking().SingleAsync(u => u.Email == email, Ct));
    }

    protected async Task<T> NoEscopoAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        await using var escopo = Ambiente.Provedor.CreateAsyncScope();
        return await acao(escopo.ServiceProvider);
    }

    protected async Task NoEscopoAsync(Func<IServiceProvider, Task> acao)
    {
        await using var escopo = Ambiente.Provedor.CreateAsyncScope();
        await acao(escopo.ServiceProvider);
    }
}

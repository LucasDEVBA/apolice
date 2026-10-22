using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Common;
using Apolice.Application.Features.Auth;
using Apolice.Infrastructure.Persistence.Migracao;
using Apolice.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Api.Suporte;

// Base dos testes de API: banco novo por classe (migrado + catálogo + usuários demo), uma API por classe (o estado do
// rate limiter não vaza entre classes) e clientes com IP próprio via X-Forwarded-For (o limite "auth" é por IP).
public abstract class ApiTeste(PostgresFixture postgres, string prefixo) : IAsyncLifetime
{
    private static int _ultimoIp;

    protected ApiFactory Api { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static JsonSerializerOptions Json => JsonOpcoes.Padrao;

    public async ValueTask InitializeAsync()
    {
        Api = new ApiFactory(await postgres.CriarBancoAsync(prefixo), Servicos);
        await using var escopo = Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<IMigradorBanco>().MigrarAsync(Ct);
        await escopo.ServiceProvider.GetRequiredService<DataSeeder>().SemearAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await Api.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected virtual void Servicos(IServiceCollection servicos)
    {
    }

    // HTTPS: o cookie de refresh é Secure. Cookies são tratados à mão nos testes de sessão (atributos e reuso explícitos).
    protected HttpClient Anonimo(string? ip = null)
    {
        var cliente = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        cliente.DefaultRequestHeaders.Add("X-Forwarded-For", ip ?? NovoIp());
        return cliente;
    }

    protected async Task<HttpClient> ComoAsync(string persona)
    {
        var cliente = Anonimo();
        var sessao = await EntrarAsync(cliente, persona);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        return cliente;
    }

    protected static async Task<AuthResponse> EntrarAsync(HttpClient cliente, string persona)
    {
        using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/demo", new { persona }, Json, Ct);
        await resposta.DeveTerStatusAsync(System.Net.HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<AuthResponse>(Json, Ct))!;
    }

    protected static string NovoIp()
    {
        var n = Interlocked.Increment(ref _ultimoIp);
        return $"10.{(n >> 16) & 0xFF}.{(n >> 8) & 0xFF}.{n & 0xFF}";
    }
}

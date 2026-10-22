using System.Net;
using System.Net.Http.Json;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Auth;

namespace Apolice.Api.IntegrationTests.Api;

// Política "auth": 10 por minuto por IP (o IP vem do X-Forwarded-For). A 11ª tentativa do mesmo IP recebe 429 com
// Retry-After em header e no ProblemDetails; outro IP segue liberado.
public sealed class LimiteRequisicoesApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_limite")
{
    private const int LimiteAuth = 10;

    [Fact]
    public async Task Deve_responder_429_com_retry_after_quando_o_ip_excede_o_limite_de_autenticacao()
    {
        var ip = NovoIp();
        using var cliente = Anonimo(ip);
        for (var tentativa = 0; tentativa < LimiteAuth; tentativa++)
        {
            using var permitida = await cliente.PostAsJsonAsync("/api/v1/auth/demo", new { persona = PersonasDemo.Corretora }, Json, Ct);
            await permitida.DeveTerStatusAsync(HttpStatusCode.OK);
        }

        using var bloqueada = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana.corretora@apolice.dev", senha = "qualquer" }, Json, Ct);

        var problema = await bloqueada.ProblemaAsync(HttpStatusCode.TooManyRequests, "Comum.LimiteDeRequisicoes");
        var retryAfter = int.Parse(bloqueada.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture);
        retryAfter.ShouldBeInRange(1, 60);
        problema.GetProperty("retryAfter").GetInt32().ShouldBe(retryAfter);

        using var outroIp = Anonimo();
        using var liberada = await outroIp.PostAsJsonAsync("/api/v1/auth/demo", new { persona = PersonasDemo.Corretora }, Json, Ct);
        await liberada.DeveTerStatusAsync(HttpStatusCode.OK);
    }

    // O refresh tem janela própria: esgotar login/demo no mesmo IP não derruba a renovação da sessão.
    [Fact]
    public async Task Deve_manter_o_refresh_liberado_quando_o_ip_esgota_o_limite_de_login()
    {
        var ip = NovoIp();
        using var cliente = Anonimo(ip);
        for (var tentativa = 0; tentativa <= LimiteAuth; tentativa++)
        {
            using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/demo", new { persona = PersonasDemo.Corretora }, Json, Ct);
        }

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("X-Requested-With", "apolice-web");
        using var renovada = await cliente.SendAsync(refresh, Ct);

        await renovada.ProblemaAsync(HttpStatusCode.Unauthorized);
    }

    // Atrás da Vercel a Function URL pode acrescentar o IP do nó de saída: o limite vale para o cliente (penúltimo
    // salto), não para o proxy compartilhado.
    [Fact]
    public async Task Deve_particionar_pelo_cliente_informado_pelo_proxy_quando_o_x_forwarded_for_tem_dois_saltos()
    {
        const string Proxy = "203.0.113.10";
        for (var tentativa = 0; tentativa < LimiteAuth; tentativa++)
        {
            using var cliente = Anonimo($"{NovoIp()}, {Proxy}");
            using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/demo", new { persona = PersonasDemo.Corretora }, Json, Ct);
            await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
        }

        using var outro = Anonimo($"{NovoIp()}, {Proxy}");
        using var liberada = await outro.PostAsJsonAsync("/api/v1/auth/demo", new { persona = PersonasDemo.Corretora }, Json, Ct);
        await liberada.DeveTerStatusAsync(HttpStatusCode.OK);
    }
}

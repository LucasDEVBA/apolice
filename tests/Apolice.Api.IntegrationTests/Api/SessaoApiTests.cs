using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Auth;

namespace Apolice.Api.IntegrationTests.Api;

// Sessão (SPEC 7): login grava o cookie de refresh com os atributos exatos; refresh exige o header anti-CSRF, rotaciona
// o token e, se um token já trocado volta, derruba a família inteira; logout revoga e apaga o cookie.
public sealed class SessaoApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_sessao")
{
    private const string Cookie = "refresh_token";

    [Fact]
    public async Task Deve_gravar_o_cookie_de_refresh_com_os_atributos_do_spec_quando_o_login_e_valido()
    {
        using var cliente = Anonimo();

        using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana.corretora@apolice.dev", senha = AmbienteInfra.SenhaSeed }, Json, Ct);

        var sessao = await resposta.LerAsync<AuthResponse>(HttpStatusCode.OK);
        sessao.Usuario.Nome.ShouldBe("Ana Ribeiro");
        sessao.AccessToken.ShouldNotBeNullOrWhiteSpace();
        var setCookie = SetCookie(resposta);
        setCookie.ShouldStartWith($"{Cookie}=");
        var atributos = Atributos(setCookie);
        atributos.ShouldContain("httponly");
        atributos.ShouldContain("secure");
        atributos.ShouldContain("samesite=strict");
        atributos.ShouldContain("path=/api/v1/auth");
        atributos.ShouldContain("max-age=604800");
        (await resposta.Content.ReadAsStringAsync(Ct)).ShouldNotContain(ValorCookie(setCookie));
    }

    [Fact]
    public async Task Deve_responder_401_com_a_mesma_mensagem_quando_a_senha_esta_errada_ou_o_email_nao_existe()
    {
        using var cliente = Anonimo();

        using var senhaErrada = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "bruno.corretor@apolice.dev", senha = "errada-123" }, Json, Ct);
        using var inexistente = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "ninguem@apolice.dev", senha = "errada-123" }, Json, Ct);

        var a = await senhaErrada.ProblemaAsync(HttpStatusCode.Unauthorized, "Auth.CredenciaisInvalidas");
        var b = await inexistente.ProblemaAsync(HttpStatusCode.Unauthorized, "Auth.CredenciaisInvalidas");
        a.GetProperty("detail").GetString().ShouldBe(b.GetProperty("detail").GetString());
        senhaErrada.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Deve_rotacionar_o_refresh_e_revogar_a_familia_quando_um_token_trocado_e_reapresentado()
    {
        using var cliente = Anonimo();
        var primeiro = await LoginDemoAsync(cliente);

        using var renovacao = await RefreshAsync(cliente, primeiro);
        var renovada = await renovacao.LerAsync<AuthResponse>(HttpStatusCode.OK);
        var segundo = ValorCookie(SetCookie(renovacao));
        segundo.ShouldNotBe(primeiro);
        renovada.Usuario.Nome.ShouldBe("Ana Ribeiro");
        await DeveAutenticarAsync(cliente, renovada.AccessToken);

        using var reuso = await RefreshAsync(cliente, primeiro);
        await reuso.ProblemaAsync(HttpStatusCode.Unauthorized, "Auth.RefreshReutilizado");
        Atributos(SetCookie(reuso)).ShouldContain("max-age=0");

        using var familiaRevogada = await RefreshAsync(cliente, segundo);
        await familiaRevogada.ProblemaAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("XMLHttpRequest")]
    public async Task Deve_responder_403_quando_o_refresh_ou_logout_nao_trazem_o_header_anti_csrf(string? valor)
    {
        using var cliente = Anonimo();
        var token = await LoginDemoAsync(cliente);

        foreach (var rota in new[] { "/api/v1/auth/refresh", "/api/v1/auth/logout" })
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, rota);
            requisicao.Headers.Add("Cookie", $"{Cookie}={token}");
            if (valor is not null)
            {
                requisicao.Headers.Add("X-Requested-With", valor);
            }

            using var resposta = await cliente.SendAsync(requisicao, Ct);
            await resposta.ProblemaAsync(HttpStatusCode.Forbidden, "Auth.CabecalhoCsrfAusente");
        }

        using var aindaValido = await RefreshAsync(cliente, token);
        await aindaValido.DeveTerStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deve_revogar_a_sessao_e_apagar_o_cookie_quando_faz_logout()
    {
        using var cliente = Anonimo();
        var token = await LoginDemoAsync(cliente);

        using var logout = await EnviarComCookieAsync(cliente, "/api/v1/auth/logout", token);
        await logout.DeveTerStatusAsync(HttpStatusCode.NoContent);
        var apagado = SetCookie(logout);
        apagado.ShouldStartWith($"{Cookie}=;");
        Atributos(apagado).ShouldContain("path=/api/v1/auth");

        using var depois = await RefreshAsync(cliente, token);
        await depois.ProblemaAsync(HttpStatusCode.Unauthorized, "Auth.RefreshInvalido");

        using var semCookie = await EnviarComCookieAsync(cliente, "/api/v1/auth/logout", null);
        await semCookie.DeveTerStatusAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deve_responder_401_quando_o_refresh_nao_traz_cookie()
    {
        using var cliente = Anonimo();

        using var resposta = await EnviarComCookieAsync(cliente, "/api/v1/auth/refresh", null);

        await resposta.ProblemaAsync(HttpStatusCode.Unauthorized, "Auth.RefreshInvalido");
    }

    private static async Task<string> LoginDemoAsync(HttpClient cliente)
    {
        using var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/demo", new { persona = PersonasDemo.Corretora }, Json, Ct);
        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
        return ValorCookie(SetCookie(resposta));
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient cliente, string token) =>
        EnviarComCookieAsync(cliente, "/api/v1/auth/refresh", token);

    private static async Task<HttpResponseMessage> EnviarComCookieAsync(HttpClient cliente, string rota, string? token)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, rota);
        requisicao.Headers.Add("X-Requested-With", "apolice-web");
        if (token is not null)
        {
            requisicao.Headers.Add("Cookie", $"{Cookie}={token}");
        }

        return await cliente.SendAsync(requisicao, Ct);
    }

    private static async Task DeveAutenticarAsync(HttpClient cliente, string accessToken)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resposta = await cliente.SendAsync(requisicao, Ct);
        (await resposta.LerAsync<UsuarioDto>(HttpStatusCode.OK)).Email.ShouldBe("ana.corretora@apolice.dev");
    }

    private static string SetCookie(HttpResponseMessage resposta) =>
        resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{Cookie}=", StringComparison.Ordinal));

    private static string ValorCookie(string setCookie) => setCookie.Split(';')[0][(Cookie.Length + 1)..];

    private static string[] Atributos(string setCookie) =>
        [.. setCookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant())];
}

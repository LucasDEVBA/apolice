using System.Net;
using System.Text.Json;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Sistema.ObterFeatures;

namespace Apolice.Api.IntegrationTests.Api;

// Transversais do pipeline: headers de segurança, correlation id, health checks, OpenAPI/Scalar e /features anônimo.
public sealed class PlataformaApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_plataforma")
{
    [Fact]
    public async Task Deve_enviar_os_headers_de_seguranca_quando_responde_a_api()
    {
        using var cliente = Anonimo();

        using var sucesso = await cliente.GetAsync("/api/v1/features", Ct);
        using var erro = await cliente.GetAsync("/api/v1/cotacoes", Ct);

        foreach (var resposta in new[] { sucesso, erro })
        {
            Header(resposta, "X-Content-Type-Options").ShouldBe("nosniff");
            Header(resposta, "X-Frame-Options").ShouldBe("DENY");
            Header(resposta, "Referrer-Policy").ShouldBe("no-referrer");
            Header(resposta, "Content-Security-Policy").ShouldBe("default-src 'none'; frame-ancestors 'none'");
            Header(resposta, "Permissions-Policy").ShouldContain("camera=()");
            resposta.Headers.CacheControl!.NoStore.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Deve_devolver_o_correlation_id_do_cliente_quando_ele_e_valido()
    {
        using var cliente = Anonimo();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cotacoes/{Guid.CreateVersion7()}");
        requisicao.Headers.Add("X-Correlation-Id", "pedido-123-abc");

        using var resposta = await cliente.SendAsync(requisicao, Ct);

        resposta.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("pedido-123-abc");
        var problema = await resposta.ProblemaAsync(HttpStatusCode.Unauthorized);
        problema.GetProperty("correlationId").GetString().ShouldBe("pedido-123-abc");
    }

    [Theory]
    [InlineData("tem espaço e acento")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public async Task Deve_gerar_outro_correlation_id_quando_o_do_cliente_e_invalido(string invalido)
    {
        using var cliente = await ComoAsync(PersonasDemo.Corretora);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/apolices/{Guid.CreateVersion7()}");
        requisicao.Headers.TryAddWithoutValidation("X-Correlation-Id", invalido).ShouldBeTrue();

        using var resposta = await cliente.SendAsync(requisicao, Ct);

        var gerado = resposta.Headers.GetValues("X-Correlation-Id").Single();
        gerado.ShouldNotBe(invalido);
        Guid.TryParse(gerado, out _).ShouldBeTrue();
        (await resposta.ProblemaAsync(HttpStatusCode.NotFound)).GetProperty("correlationId").GetString().ShouldBe(gerado);
    }

    [Fact]
    public async Task Deve_responder_saudavel_quando_os_health_checks_rodam()
    {
        using var cliente = Anonimo();

        using var live = await cliente.GetAsync("/health/live", Ct);
        using var ready = await cliente.GetAsync("/health/ready", Ct);

        await live.DeveTerStatusAsync(HttpStatusCode.OK);
        await ready.DeveTerStatusAsync(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Deve_expor_as_features_sem_autenticacao()
    {
        using var cliente = Anonimo();

        using var resposta = await cliente.GetAsync("/api/v1/features", Ct);

        var features = await resposta.LerAsync<FeaturesDto>(HttpStatusCode.OK);
        features.ShouldBe(new FeaturesDto(true, true, true, true));
    }

    [Fact]
    public async Task Deve_publicar_o_documento_openapi_com_todas_as_rotas_do_spec()
    {
        using var cliente = Anonimo();

        using var resposta = await cliente.GetAsync("/openapi/v1.json", Ct);

        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
        var caminhos = documento.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject().Select(o => $"{o.Name.ToUpperInvariant()} {p.Name}"))
            .ToHashSet(StringComparer.Ordinal);
        caminhos.Count.ShouldBe(RotasDoSpec.Length);
        caminhos.ShouldBe(RotasDoSpec, ignoreOrder: true);

        var raiz = documento.RootElement;
        raiz.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString().ShouldBe("bearer");
        var dadosRisco = raiz.GetProperty("components").GetProperty("schemas").GetProperty("DadosRisco");
        dadosRisco.GetProperty("oneOf").GetArrayLength().ShouldBe(3);
        dadosRisco.GetProperty("discriminator").GetProperty("propertyName").GetString().ShouldBe("produto");
        raiz.GetProperty("paths").GetProperty("/api/v1/features").GetProperty("get").TryGetProperty("security", out _).ShouldBeFalse();
        raiz.GetProperty("paths").GetProperty("/api/v1/apolices").GetProperty("get").GetProperty("security").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Deve_servir_o_scalar_com_csp_de_mesma_origem()
    {
        using var cliente = Anonimo();

        using var resposta = await cliente.GetAsync("/scalar/v1", Ct);

        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        var csp = Header(resposta, "Content-Security-Policy");
        csp.ShouldContain("default-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
    }

    private static readonly string[] RotasDoSpec =
    [
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/demo",
        "POST /api/v1/auth/refresh",
        "POST /api/v1/auth/logout",
        "GET /api/v1/auth/me",
        "GET /api/v1/features",
        "GET /api/v1/produtos",
        "GET /api/v1/produtos/{codigo}",
        "PUT /api/v1/produtos/{codigo}/coberturas/{coberturaCodigo}/taxa",
        "POST /api/v1/cotacoes/simular",
        "POST /api/v1/cotacoes",
        "GET /api/v1/cotacoes",
        "GET /api/v1/cotacoes/{id}",
        "POST /api/v1/cotacoes/{id}/aceitar",
        "POST /api/v1/cotacoes/{id}/recusar",
        "POST /api/v1/apolices",
        "GET /api/v1/apolices",
        "GET /api/v1/apolices/{id}",
        "GET /api/v1/apolices/{id}/documento",
        "POST /api/v1/apolices/{id}/endossos/simular",
        "POST /api/v1/apolices/{id}/endossos",
        "POST /api/v1/apolices/{id}/cancelamento/simular",
        "POST /api/v1/apolices/{id}/cancelamento",
        "POST /api/v1/sinistros",
        "GET /api/v1/sinistros",
        "GET /api/v1/sinistros/{id}",
        "POST /api/v1/sinistros/{id}/analise",
        "POST /api/v1/sinistros/{id}/aprovacao",
        "POST /api/v1/sinistros/{id}/negativa",
        "POST /api/v1/sinistros/{id}/pagamento",
        "GET /api/v1/dashboard",
        "GET /api/v1/admin/auditoria",
    ];

    private static string Header(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores) ? valores.Single() : resposta.Content.Headers.GetValues(nome).Single();
}

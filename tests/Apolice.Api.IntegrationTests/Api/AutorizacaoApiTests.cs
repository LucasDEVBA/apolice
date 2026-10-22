using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes;
using Apolice.Domain.Produtos;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Apolice.Api.IntegrationTests.Api;

// 401 (sem token, token adulterado, chave ou algoritmo errados), 403 (política de papel) e 404 anti-BOLA: o recurso de
// outro corretor responde exatamente como um inexistente.
public sealed class AutorizacaoApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_autorizacao")
{
    [Theory]
    [InlineData("/api/v1/cotacoes")]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/produtos")]
    [InlineData("/api/v1/dashboard")]
    public async Task Deve_responder_401_quando_nao_ha_token(string rota)
    {
        using var cliente = Anonimo();

        using var resposta = await cliente.GetAsync(rota, Ct);

        await resposta.ProblemaAsync(HttpStatusCode.Unauthorized, "Auth.NaoAutenticado");
        resposta.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task Deve_responder_401_quando_o_token_foi_adulterado()
    {
        using var cliente = Anonimo();
        var sessao = await EntrarAsync(cliente, PersonasDemo.Corretora);
        var partes = sessao.AccessToken.Split('.');
        var payloadAdulterado = Base64UrlEncoder.Encode(Base64UrlEncoder.Decode(partes[1]).Replace("\"Corretor\"", "\"Admin\"", StringComparison.Ordinal));
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{partes[0]}.{payloadAdulterado}.{partes[2]}");

        using var resposta = await cliente.GetAsync("/api/v1/admin/auditoria", Ct);

        await resposta.ProblemaAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("outra-chave-de-assinatura-com-bem-mais-de-32-bytes", SecurityAlgorithms.HmacSha256)]
    [InlineData(AmbienteInfra.ChaveJwtTeste, SecurityAlgorithms.HmacSha384)]
    public async Task Deve_responder_401_quando_a_chave_ou_o_algoritmo_nao_sao_os_da_api(string chave, string algoritmo)
    {
        var admin = await UsuarioAsync(PersonasDemo.Admin);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "apolice-api",
            Audience = "apolice-web",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)), algoritmo),
            Claims = new Dictionary<string, object> { ["sub"] = admin.Id.ToString(), ["role"] = "Admin", ["name"] = admin.Nome },
        });
        using var cliente = Anonimo();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resposta = await cliente.GetAsync("/api/v1/admin/auditoria", Ct);

        await resposta.ProblemaAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deve_responder_403_quando_o_papel_nao_atende_a_politica()
    {
        using var corretora = await ComoAsync(PersonasDemo.Corretora);
        using var analista = await ComoAsync(PersonasDemo.Analista);

        using var taxa = await corretora.PutAsJsonAsync($"/api/v1/produtos/AUTO/coberturas/{CodigosCobertura.Casco}/taxa", new { taxa = 0.05m }, Json, Ct);
        using var auditoria = await corretora.GetAsync("/api/v1/admin/auditoria", Ct);
        using var cotacao = await analista.PostAsJsonAsync("/api/v1/cotacoes", Pedidos.CotacaoAuto(), Json, Ct);

        await taxa.ProblemaAsync(HttpStatusCode.Forbidden, "Comum.Proibido");
        await auditoria.ProblemaAsync(HttpStatusCode.Forbidden, "Comum.Proibido");
        await cotacao.ProblemaAsync(HttpStatusCode.Forbidden, "Comum.Proibido");
    }

    [Fact]
    public async Task Deve_responder_403_quando_o_corretor_dono_tenta_decidir_o_proprio_sinistro()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var sinistro = await Operacoes.SinistroAbertoAsync(ana, apolice.Id);

        using var resposta = await ana.PostAsync($"/api/v1/sinistros/{sinistro.Id}/analise", null, Ct);

        await resposta.ProblemaAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deve_responder_404_quando_corretor_acessa_recurso_de_outro_corretor()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        using var bruno = await ComoAsync(PersonasDemo.Corretor);
        var cotacao = await Operacoes.CriarCotacaoAsync(ana);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var sinistro = await Operacoes.SinistroAbertoAsync(ana, apolice.Id);

        string[] leituras =
        [
            $"/api/v1/cotacoes/{cotacao.Id}",
            $"/api/v1/apolices/{apolice.Id}",
            $"/api/v1/apolices/{apolice.Id}/documento",
            $"/api/v1/sinistros/{sinistro.Id}",
        ];
        foreach (var rota in leituras)
        {
            using var resposta = await bruno.GetAsync(rota, Ct);
            await resposta.ProblemaAsync(HttpStatusCode.NotFound, "Comum.NaoEncontrado");
        }

        using var aceite = await bruno.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/aceitar", null, Ct);
        await aceite.ProblemaAsync(HttpStatusCode.NotFound, "Comum.NaoEncontrado");
        using var cancelamento = await bruno.PostAsJsonAsync($"/api/v1/apolices/{apolice.Id}/cancelamento", new { motivo = "PedidoDoSegurado" }, Json, Ct);
        await cancelamento.ProblemaAsync(HttpStatusCode.NotFound, "Comum.NaoEncontrado");
        using var sinistroAlheio = await Operacoes.AbrirSinistroAsync(bruno, Operacoes.Sinistro(apolice.Id));
        await sinistroAlheio.ProblemaAsync(HttpStatusCode.NotFound, "Comum.NaoEncontrado");

        using var inexistente = await bruno.GetAsync($"/api/v1/apolices/{Guid.CreateVersion7()}", Ct);
        var corpoInexistente = await inexistente.ProblemaAsync(HttpStatusCode.NotFound, "Comum.NaoEncontrado");
        using var alheia = await bruno.GetAsync($"/api/v1/apolices/{apolice.Id}", Ct);
        var corpoAlheia = await alheia.ProblemaAsync(HttpStatusCode.NotFound);
        corpoAlheia.GetProperty("detail").GetString().ShouldBe(corpoInexistente.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Deve_listar_so_a_propria_carteira_quando_o_usuario_e_corretor()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        using var bruno = await ComoAsync(PersonasDemo.Corretor);
        using var admin = await ComoAsync(PersonasDemo.Admin);
        var apolice = await Operacoes.ApoliceAsync(ana);

        using var deBruno = await bruno.GetAsync("/api/v1/apolices", Ct);
        using var deAdmin = await admin.GetAsync("/api/v1/apolices?limite=100", Ct);

        (await deBruno.LerAsync<Pagina<ApoliceResumoDto>>(HttpStatusCode.OK)).Itens.ShouldBeEmpty();
        var global = await deAdmin.LerAsync<Pagina<ApoliceResumoDto>>(HttpStatusCode.OK);
        global.Itens.ShouldContain(a => a.Id == apolice.Id);
        global.Itens.Single(a => a.Id == apolice.Id).SeguradoCpfMascarado.ShouldBe("***.982.247-**");
    }

    [Fact]
    public async Task Deve_permitir_que_admin_opere_qualquer_carteira()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        using var admin = await ComoAsync(PersonasDemo.Admin);
        var cotacao = await Operacoes.CriarCotacaoAsync(ana);

        using var resposta = await admin.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/recusar", null, Ct);

        (await resposta.LerAsync<CotacaoDto>(HttpStatusCode.OK)).Status.ShouldBe(Domain.Cotacoes.StatusEfetivoCotacao.Recusada);
    }

    private async Task<UsuarioDto> UsuarioAsync(string persona)
    {
        using var cliente = await ComoAsync(persona);
        return (await cliente.GetFromJsonAsync<UsuarioDto>("/api/v1/auth/me", Json, Ct))!;
    }
}

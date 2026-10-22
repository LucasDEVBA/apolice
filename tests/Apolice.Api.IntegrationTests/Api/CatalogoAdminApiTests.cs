using System.Net;
using System.Net.Http.Json;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Common;
using Apolice.Application.Features.Auditoria;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Produtos;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;

namespace Apolice.Api.IntegrationTests.Api;

// Catálogo (leitura em cache + atualização de taxa pelo Admin, com invalidação), listagens com filtro/paginação e a
// trilha de auditoria carregando o correlation id da requisição que a gerou.
public sealed class CatalogoAdminApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_catalogo")
{
    [Fact]
    public async Task Deve_atualizar_a_taxa_e_auditar_com_o_correlation_id_quando_o_admin_altera_o_catalogo()
    {
        using var admin = await ComoAsync(PersonasDemo.Admin);
        using var antes = await admin.GetAsync("/api/v1/produtos", Ct);
        (await antes.LerAsync<List<ProdutoDto>>(HttpStatusCode.OK)).Select(p => p.Codigo).ShouldBe([CodigosProduto.Auto, CodigosProduto.Residencial, CodigosProduto.Vida], ignoreOrder: true);

        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/produtos/auto/coberturas/{CodigosCobertura.Casco}/taxa")
        {
            Content = JsonContent.Create(new { taxa = 0.035m }),
        };
        requisicao.Headers.Add("X-Correlation-Id", "ajuste-taxa-casco");
        using var atualizacao = await admin.SendAsync(requisicao, Ct);
        (await atualizacao.LerAsync<ProdutoDto>(HttpStatusCode.OK)).Coberturas.Single(c => c.Codigo == CodigosCobertura.Casco).Taxa.ShouldBe(0.035m);

        using var depois = await admin.GetAsync("/api/v1/produtos/AUTO", Ct);
        (await depois.LerAsync<ProdutoDto>(HttpStatusCode.OK)).Coberturas.Single(c => c.Codigo == CodigosCobertura.Casco).Taxa.ShouldBe(0.035m);

        using var auditoria = await admin.GetAsync("/api/v1/admin/auditoria?limite=5", Ct);
        var registro = (await auditoria.LerAsync<Pagina<AuditoriaDto>>(HttpStatusCode.OK)).Itens.First(a => a.Acao == "produto.taxa_atualizada");
        registro.CorrelationId.ShouldBe("ajuste-taxa-casco");
        registro.UsuarioNome.ShouldBe("Diego Martins");
    }

    [Fact]
    public async Task Deve_responder_404_quando_o_produto_nao_existe()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);

        using var resposta = await ana.GetAsync("/api/v1/produtos/BARCO", Ct);

        await resposta.ProblemaAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deve_filtrar_e_paginar_por_cursor_quando_lista_cotacoes()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var criadas = new List<CotacaoDto>();
        for (var i = 0; i < 3; i++)
        {
            criadas.Add(await Operacoes.CriarCotacaoAsync(ana));
        }

        using var aceite = await ana.PostAsync($"/api/v1/cotacoes/{criadas[0].Id}/aceitar", null, Ct);
        await aceite.DeveTerStatusAsync(HttpStatusCode.OK);

        using var primeira = await ana.GetAsync("/api/v1/cotacoes?status=Calculada&produto=auto&limite=1", Ct);
        var paginaUm = await primeira.LerAsync<Pagina<CotacaoResumoDto>>(HttpStatusCode.OK);
        paginaUm.Itens.ShouldHaveSingleItem().Status.ShouldBe(StatusEfetivoCotacao.Calculada);
        paginaUm.ProximoCursor.ShouldNotBeNull();

        using var segunda = await ana.GetAsync($"/api/v1/cotacoes?status=Calculada&limite=1&cursor={paginaUm.ProximoCursor}", Ct);
        var paginaDois = await segunda.LerAsync<Pagina<CotacaoResumoDto>>(HttpStatusCode.OK);
        paginaDois.Itens.ShouldHaveSingleItem().Id.ShouldNotBe(paginaUm.Itens[0].Id);
        new[] { paginaUm.Itens[0].Id, paginaDois.Itens[0].Id }.ShouldBe([criadas[2].Id, criadas[1].Id]);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.SimularCancelamento;
using Apolice.Application.Features.Apolices.SimularEndosso;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Dashboard;
using Apolice.Application.Features.Sinistros;
using Apolice.Domain.Apolices;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Api.IntegrationTests.Api;

// O ciclo de vida inteiro pela API real: cotação → emissão → endosso → sinistro → cancelamento → análise →
// aprovação → pagamento, com os consumidores de evento (InMemory + despacho imediato) gerando notificações e triagem.
public sealed class FluxoCompletoApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_fluxo")
{
    [Fact]
    public async Task Deve_percorrer_o_ciclo_de_vida_quando_corretora_e_analista_operam_pela_api()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        using var carla = await ComoAsync(PersonasDemo.Analista);

        using var simulacao = await ana.PostAsJsonAsync("/api/v1/cotacoes/simular", Pedidos.CotacaoAuto(), Json, Ct);
        var tarifacao = await simulacao.LerAsync<TarifacaoDto>(HttpStatusCode.OK);
        tarifacao.PremioTotal.ShouldBeGreaterThan(0m);

        using var criacao = await ana.PostAsJsonAsync("/api/v1/cotacoes", Pedidos.CotacaoAuto(), Json, Ct);
        var cotacao = await criacao.LerAsync<CotacaoDto>(HttpStatusCode.Created);
        criacao.Headers.Location!.OriginalString.ShouldBe($"/api/v1/cotacoes/{cotacao.Id}");
        cotacao.Status.ShouldBe(StatusEfetivoCotacao.Calculada);
        cotacao.PremioTotal.ShouldBe(tarifacao.PremioTotal);
        cotacao.CorretorNome.ShouldBe("Ana Ribeiro");

        using var aceite = await ana.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/aceitar", null, Ct);
        (await aceite.LerAsync<CotacaoDto>(HttpStatusCode.OK)).Status.ShouldBe(StatusEfetivoCotacao.Aceita);

        using var emissao = await Operacoes.EmitirAsync(ana, Operacoes.Emissao(cotacao.Id), Guid.NewGuid().ToString());
        var apolice = await emissao.LerAsync<ApoliceDto>(HttpStatusCode.Created);
        emissao.Headers.Location!.OriginalString.ShouldBe($"/api/v1/apolices/{apolice.Id}");
        apolice.Numero.ShouldStartWith($"AUTO-{Pedidos.Hoje.Year}-");
        apolice.Status.ShouldBe(StatusEfetivoApolice.Ativa);
        apolice.CronogramaParcelas.Count.ShouldBe(Operacoes.Parcelas);
        apolice.Segurado.Cpf.ShouldBe("529.982.247-25");

        var endossada = await EndossarAsync(ana, apolice.Id);
        var sinistro = await Operacoes.SinistroAbertoAsync(ana, apolice.Id);
        sinistro.Status.ShouldBe(StatusSinistro.Aberto);
        sinistro.Triagem.ShouldNotBeNull();
        sinistro.Triagem.Alertas.ShouldContain(AlertasTriagem.VigenciaRecente);

        await CancelarAsync(ana, apolice.Id, endossada.PremioLiquidoVigente);
        await DecidirSinistroAsync(carla, sinistro.Id);

        using var detalhe = await ana.GetAsync($"/api/v1/apolices/{apolice.Id}", Ct);
        var final = await detalhe.LerAsync<ApoliceDto>(HttpStatusCode.OK);
        final.Status.ShouldBe(StatusEfetivoApolice.Cancelada);
        final.Endossos.ShouldHaveSingleItem().CriadoPorNome.ShouldBe("Ana Ribeiro");
        final.Sinistros.ShouldHaveSingleItem().Status.ShouldBe(StatusSinistro.Pago);
        final.BoasVindasEnviadasEm.ShouldNotBeNull();
        final.Notificacoes.Select(n => n.Tipo).Distinct().ShouldBe(
            [TipoNotificacao.BoasVindas, TipoNotificacao.Endosso, TipoNotificacao.SinistroAberto, TipoNotificacao.Cancelamento, TipoNotificacao.SinistroDecidido],
            ignoreOrder: true);

        await DeveBaixarPdfAsync(ana, final);
        await DashboardDeveRefletirAsync(ana, carla);
    }

    private static async Task<ApoliceDto> EndossarAsync(HttpClient cliente, Guid apoliceId)
    {
        var pedido = new EndossoRequest(TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null);
        using var simulacao = await cliente.PostAsJsonAsync($"/api/v1/apolices/{apoliceId}/endossos/simular", pedido, Json, Ct);
        var simulado = await simulacao.LerAsync<SimulacaoEndossoDto>(HttpStatusCode.OK);
        simulado.AjusteTotal.ShouldBeGreaterThan(0m);

        using var endosso = await cliente.PostAsJsonAsync($"/api/v1/apolices/{apoliceId}/endossos", pedido, Json, Ct);
        var endossada = await endosso.LerAsync<ApoliceDto>(HttpStatusCode.Created);
        endosso.Headers.Location!.OriginalString.ShouldBe($"/api/v1/apolices/{apoliceId}");
        endossada.Coberturas.Select(c => c.Codigo).ShouldContain(CodigosCobertura.Vidros);
        endossada.Endossos.ShouldHaveSingleItem().AjusteTotal.ShouldBe(simulado.AjusteTotal);
        return endossada;
    }

    private static async Task CancelarAsync(HttpClient cliente, Guid apoliceId, decimal premioVigente)
    {
        var pedido = new CancelamentoRequest(MotivoCancelamento.PedidoDoSegurado);
        using var simulacao = await cliente.PostAsJsonAsync($"/api/v1/apolices/{apoliceId}/cancelamento/simular", pedido, Json, Ct);
        var simulado = await simulacao.LerAsync<SimulacaoCancelamentoDto>(HttpStatusCode.OK);
        simulado.Metodo.ShouldBe(MetodoCancelamento.PrazoCurto);
        simulado.PremioLiquidoBase.ShouldBe(premioVigente);

        using var cancelamento = await cliente.PostAsJsonAsync($"/api/v1/apolices/{apoliceId}/cancelamento", pedido, Json, Ct);
        var cancelada = await cancelamento.LerAsync<ApoliceDto>(HttpStatusCode.OK);
        cancelada.Status.ShouldBe(StatusEfetivoApolice.Cancelada);
        cancelada.ValorRestituicao.ShouldBe(simulado.ValorRestituicao);
    }

    private static async Task DecidirSinistroAsync(HttpClient analista, Guid sinistroId)
    {
        using var analise = await analista.PostAsync($"/api/v1/sinistros/{sinistroId}/analise", null, Ct);
        (await analise.LerAsync<SinistroDto>(HttpStatusCode.OK)).Status.ShouldBe(StatusSinistro.EmAnalise);

        using var aprovacao = await analista.PostAsJsonAsync($"/api/v1/sinistros/{sinistroId}/aprovacao", new { valorIndenizacao = 5_000m, observacao = "Laudo confirma o dano." }, Json, Ct);
        var aprovado = await aprovacao.LerAsync<SinistroDto>(HttpStatusCode.OK);
        aprovado.ValorIndenizacao.ShouldBe(5_000m);

        using var pagamento = await analista.PostAsync($"/api/v1/sinistros/{sinistroId}/pagamento", null, Ct);
        var pago = await pagamento.LerAsync<SinistroDto>(HttpStatusCode.OK);
        pago.Status.ShouldBe(StatusSinistro.Pago);
        pago.Historico.Select(h => h.UsuarioNome).ShouldBe(["Ana Ribeiro", "Carla Souza", "Carla Souza", "Carla Souza"]);
    }

    private static async Task DeveBaixarPdfAsync(HttpClient cliente, ApoliceDto apolice)
    {
        using var resposta = await cliente.GetAsync($"/api/v1/apolices/{apolice.Id}/documento", Ct);
        await resposta.DeveTerStatusAsync(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        resposta.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        resposta.Content.Headers.ContentDisposition.FileName.ShouldBe($"apolice-{apolice.Numero}.pdf");
        resposta.Headers.CacheControl!.NoStore.ShouldBeTrue();

        var pdf = await resposta.Content.ReadAsByteArrayAsync(Ct);
        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        Documentos.DeveParecerPdf(pdf, "apolice-api.pdf");
    }

    private static async Task DashboardDeveRefletirAsync(HttpClient corretora, HttpClient analista)
    {
        using var carteira = await corretora.GetAsync("/api/v1/dashboard", Ct);
        (await carteira.LerAsync<DashboardDto>(HttpStatusCode.OK)).Escopo.ShouldBe(EscopoDashboard.Carteira);

        using var global = await analista.GetAsync("/api/v1/dashboard", Ct);
        (await global.LerAsync<DashboardDto>(HttpStatusCode.OK)).Escopo.ShouldBe(EscopoDashboard.Global);
    }
}

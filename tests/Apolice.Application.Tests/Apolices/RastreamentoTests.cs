using System.Diagnostics;
using Apolice.Application.Abstractions.Observability;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Produtos;

namespace Apolice.Application.Tests.Apolices;

// Spans do ActivitySource "Apolice": nome, tag de produto e status de erro nos casos de uso instrumentados.
[Collection(nameof(TelemetriaGlobal))]
public sealed class RastreamentoTests : TesteBase
{
    [Fact]
    public async Task Deve_registrar_span_com_produto_quando_emite_e_marcar_erro_quando_emissao_recusada()
    {
        var aceita = await Amb.CotacaoAceitaAsync(Amb.Ana);
        var outra = await Amb.CotacaoAceitaAsync(Amb.Ana);
        var spans = new List<Activity>();
        using var ouvinte = Ouvir(spans);

        (await Amb.Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(aceita.Id, Amb.Hoje, 1), Ct)).DeveTerSucesso();
        await Amb.Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(outra.Id, Amb.Hoje, 99), Ct);

        spans.Select(s => s.OperationName).ShouldBe([ApoliceTelemetry.AtividadeEmitirApolice, ApoliceTelemetry.AtividadeEmitirApolice]);
        spans.ShouldAllBe(s => (string?)s.GetTagItem(ApoliceTelemetry.TagProduto) == CodigosProduto.Auto);
        spans[0].Status.ShouldBe(ActivityStatusCode.Unset);
        spans[1].Status.ShouldBe(ActivityStatusCode.Error);
        spans[1].StatusDescription.ShouldBe("Apolice.ParcelasInvalidas");
    }

    [Fact]
    public async Task Deve_marcar_erro_no_span_quando_aviso_de_sinistro_recusado()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        var spans = new List<Activity>();
        using var ouvinte = Ouvir(spans);

        await Amb.Servico<AbrirSinistroHandler>().HandleAsync(new AbrirSinistroCommand(apolice.Id, CodigosCobertura.Casco, Amb.Hoje.AddDays(1), Ambiente.DescricaoSinistro, 100m), Ct);

        var span = spans.ShouldHaveSingleItem();
        span.OperationName.ShouldBe(ApoliceTelemetry.AtividadeAbrirSinistro);
        span.GetTagItem(ApoliceTelemetry.TagProduto).ShouldBe(CodigosProduto.Auto);
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBe("Sinistro.OcorrenciaFutura");
    }

    [Fact]
    public async Task Deve_registrar_span_de_consumo_com_tipo_e_mensagem_quando_dispatcher_entrega_evento()
    {
        await Amb.EmitirApoliceAsync(Amb.Ana);
        var evento = Amb.Banco.Outbox.Single();
        var spans = new List<Activity>();
        using var ouvinte = Ouvir(spans);

        await Amb.ProcessarOutboxAsync();

        var span = spans.ShouldHaveSingleItem();
        span.OperationName.ShouldBe($"{ApoliceTelemetry.AtividadeConsumirEvento} ApoliceEmitida");
        span.GetTagItem(ApoliceTelemetry.TagMensagemId).ShouldBe(evento.EventoId);
        span.GetTagItem(ApoliceTelemetry.TagTipoMensagem).ShouldBe("ApoliceEmitida");
    }

    private static ActivityListener Ouvir(List<Activity> spans)
    {
        var ouvinte = new ActivityListener
        {
            ShouldListenTo = fonte => fonte.Name == ApoliceTelemetry.Nome,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(ouvinte);
        return ouvinte;
    }
}

using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices.Cancelar;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Apolices.Obter;
using Apolice.Application.Features.Apolices.SimularCancelamento;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Criar;
using Apolice.Application.Features.Cotacoes.Simular;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.IniciarAnalise;
using Apolice.Application.Features.Sinistros.Obter;
using Apolice.Application.Features.Sinistros.RegistrarPagamento;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Fluxo;

// Ponta a ponta na camada de aplicação: corretora cota, emite, endossa e cancela; o sinistro de antes do
// cancelamento é aberto, triado, analisado, aprovado e pago — com cada evento passando pelo dispatcher.
public sealed class FluxoCompletoTests : TesteBase
{
    [Fact]
    public async Task Deve_concluir_o_ciclo_de_vida_quando_cotacao_vira_apolice_endossada_cancelada_e_sinistro_pago()
    {
        Amb.Como(Amb.Ana);
        var simulacao = (await Amb.Servico<SimularCotacaoHandler>().HandleAsync(new SimularCotacaoCommand(Dados.CotacaoAuto()), Ct)).DeveTerSucesso();
        var cotacao = (await Amb.Servico<CriarCotacaoHandler>().HandleAsync(new CriarCotacaoCommand(Dados.CotacaoAuto()), Ct)).DeveTerSucesso();
        (await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct)).DeveTerSucesso();
        var apolice = (await Amb.Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 3), Ct)).DeveTerSucesso();
        await Amb.ProcessarOutboxAsync();
        simulacao.PremioTotal.ShouldBe(cotacao.PremioTotal);

        // Dia 15: inclusão de VIDROS (220 × 350/365 = 210,96 + IOF 15,57).
        Amb.Avancar(TimeSpan.FromDays(15));
        var endossada = (await Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct)).DeveTerSucesso();
        endossada.PremioLiquidoVigente.ShouldBe(4_010.96m);
        await Amb.ProcessarOutboxAsync();

        // Dia 35: pedido do segurado → prazo curto, linha de 45 dias (27% retido) sobre o prêmio vigente.
        Amb.Avancar(TimeSpan.FromDays(20));
        var simulada = (await Amb.Servico<SimularCancelamentoHandler>().HandleAsync(new SimularCancelamentoCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct)).DeveTerSucesso();
        var cancelada = (await Amb.Servico<CancelarApoliceHandler>().HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct)).DeveTerSucesso();
        simulada.PercentualRetido.ShouldBe(0.27m);
        cancelada.ValorRestituicao.ShouldBe(simulada.ValorRestituicao);
        cancelada.ValorRestituicao.ShouldBe(2_928.00m);
        await Amb.ProcessarOutboxAsync();

        // Dia 40: aviso de sinistro ocorrido no dia 20 (antes do cancelamento) — ainda indenizável.
        Amb.Avancar(TimeSpan.FromDays(5));
        var aviso = new AbrirSinistroCommand(apolice.Id, CodigosCobertura.Casco, apolice.VigenciaInicio.AddDays(20), Ambiente.DescricaoSinistro, 12_000m);
        var sinistro = (await Amb.Servico<AbrirSinistroHandler>().HandleAsync(aviso, Ct)).DeveTerSucesso();
        await Amb.ProcessarOutboxAsync();

        Amb.Como(Amb.Carla);
        (await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(sinistro.Id), Ct)).DeveTerSucesso();
        (await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(sinistro.Id, 10_000m, "Laudo da oficina conferido."), Ct)).DeveTerSucesso();
        var pago = (await Amb.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(sinistro.Id), Ct)).DeveTerSucesso();
        await Amb.ProcessarOutboxAsync();

        pago.Status.ShouldBe(StatusSinistro.Pago);
        pago.Triagem!.Score.ShouldBe(40);

        Amb.Como(Amb.Ana);
        var final = (await Amb.Servico<ObterApoliceHandler>().HandleAsync(new ObterApoliceQuery(apolice.Id), Ct)).DeveTerSucesso();
        final.Status.ShouldBe(StatusEfetivoApolice.Cancelada);
        final.Endossos.ShouldHaveSingleItem().CoberturaCodigo.ShouldBe(CodigosCobertura.Vidros);
        final.Sinistros.ShouldHaveSingleItem().Status.ShouldBe(StatusSinistro.Pago);
        final.BoasVindasEnviadasEm.ShouldNotBeNull();

        // O que os consumidores gravaram (a junção apólice + sinistros do detalhe é da consulta da infra).
        Amb.Banco.Notificacoes.Select(n => (n.Tipo, n.ReferenciaId)).ShouldBe(
        [
            (TipoNotificacao.BoasVindas, apolice.Id), (TipoNotificacao.Endosso, apolice.Id), (TipoNotificacao.Cancelamento, apolice.Id),
            (TipoNotificacao.SinistroAberto, sinistro.Id), (TipoNotificacao.SinistroDecidido, sinistro.Id), (TipoNotificacao.SinistroDecidido, sinistro.Id),
        ]);
        (await Amb.Servico<ObterSinistroHandler>().HandleAsync(new ObterSinistroQuery(sinistro.Id), Ct)).DeveTerSucesso().ValorIndenizacao.ShouldBe(10_000m);
        Amb.Auditoria.Acoes.ShouldBe(
        [
            AcoesAuditoria.CotacaoCriada, AcoesAuditoria.CotacaoAceita, AcoesAuditoria.ApoliceEmitida, AcoesAuditoria.ApoliceEndossada,
            AcoesAuditoria.ApoliceCancelada, AcoesAuditoria.SinistroAberto, AcoesAuditoria.SinistroTriado, AcoesAuditoria.SinistroEmAnalise,
            AcoesAuditoria.SinistroAprovado, AcoesAuditoria.SinistroPago,
        ]);
        Amb.Banco.Outbox.ShouldBeEmpty();
    }
}

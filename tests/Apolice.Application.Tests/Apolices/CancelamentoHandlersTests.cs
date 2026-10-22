using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices.Cancelar;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Apolices.SimularCancelamento;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;

namespace Apolice.Application.Tests.Apolices;

public sealed class CancelamentoHandlersTests : TesteBase
{
    [Fact]
    public async Task Deve_aplicar_prazo_curto_quando_simula_pedido_do_segurado()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(20));

        var resultado = await Amb.Servico<SimularCancelamentoHandler>()
            .HandleAsync(new SimularCancelamentoCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct);

        // 20 dias → linha de 30 dias da tabela (20% retido): 3.800,00 × 0,80 = 3.040,00.
        resultado.DeveTerSucesso().ShouldBe(new SimulacaoCancelamentoDto(new DateOnly(2026, 3, 30), 20, 365, MetodoCancelamento.PrazoCurto, 0.20m, 3_800m, 3_040.00m));
        Amb.Banco.Apolices.Single().Status.ShouldBe(StatusApolice.Ativa);
    }

    [Fact]
    public async Task Deve_cancelar_pro_rata_auditar_e_devolver_apolice_quando_inadimplencia()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(73));

        var resultado = await Amb.Servico<CancelarApoliceHandler>()
            .HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.Inadimplencia), Ct);

        var cancelada = resultado.DeveTerSucesso();
        cancelada.Status.ShouldBe(StatusEfetivoApolice.Cancelada);
        cancelada.MotivoCancelamento.ShouldBe(MotivoCancelamento.Inadimplencia);
        cancelada.ValorRestituicao.ShouldBe(3_040.00m);
        cancelada.CanceladaEm.ShouldBe(Amb.Agora);
        Amb.Auditoria.Registros.Last().Detalhes.DeveSerJsonEquivalente("""{"motivo":"Inadimplencia","metodo":"ProRataDie","valorRestituicao":3040}""");
        Amb.Banco.Outbox.OfType<ApoliceCancelada>().ShouldHaveSingleItem();
        Amb.Auditoria.Acoes.Last().ShouldBe(AcoesAuditoria.ApoliceCancelada);
    }

    [Fact]
    public async Task Deve_restituir_integral_quando_cancela_antes_do_inicio()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);
        var apolice = (await Amb.Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje.AddDays(10), 1), Ct)).DeveTerSucesso();

        var simulacao = (await Amb.Servico<SimularCancelamentoHandler>()
            .HandleAsync(new SimularCancelamentoCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct)).DeveTerSucesso();

        apolice.Status.ShouldBe(StatusEfetivoApolice.Programada);
        simulacao.Metodo.ShouldBe(MetodoCancelamento.Integral);
        simulacao.ValorRestituicao.ShouldBe(Dados.PremioLiquidoAuto);
    }

    [Fact]
    public async Task Deve_retornar_status_invalido_quando_cancela_duas_vezes()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        var handler = Amb.Servico<CancelarApoliceHandler>();
        await handler.HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct);

        var segunda = await handler.HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct);
        var simulacao = await Amb.Servico<SimularCancelamentoHandler>().HandleAsync(new SimularCancelamentoCommand(apolice.Id, MotivoCancelamento.Inadimplencia), Ct);

        segunda.DeveFalharCom("Apolice.StatusInvalido", ErrorType.RegraDeNegocio);
        simulacao.DeveFalharCom("Apolice.StatusInvalido", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public async Task Deve_rejeitar_motivo_fora_do_enum_quando_cancela()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var resultado = await Amb.Servico<CancelarApoliceHandler>().HandleAsync(new CancelarApoliceCommand(apolice.Id, (MotivoCancelamento)99), Ct);

        resultado.DeveFalharCom("Apolice.MotivoCancelamentoInvalido", ErrorType.Validacao);
    }

    [Fact]
    public async Task Deve_proibir_analista_quando_tenta_cancelar()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Como(Amb.Carla);

        var resultado = await Amb.Servico<CancelarApoliceHandler>().HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.PedidoDoSegurado), Ct);

        resultado.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
    }
}

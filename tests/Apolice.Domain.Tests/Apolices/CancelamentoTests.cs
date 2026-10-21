using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Apolices;

// Apólice AUTO base: líquido 3.800,00, vigência 10/03/2026 → 10/03/2027 (365 dias). IOF nunca é restituído.
public sealed class CancelamentoTests
{
    private static readonly DateOnly Inicio = new(2026, 3, 10);

    [Theory]
    [InlineData(MotivoCancelamento.PedidoDoSegurado)]
    [InlineData(MotivoCancelamento.IniciativaSeguradora)]
    [InlineData(MotivoCancelamento.Inadimplencia)]
    public void Deve_restituir_integral_quando_cancela_antes_do_inicio(MotivoCancelamento motivo)
    {
        // Emitida em 10/03 com início em 20/03 e cancelada no próprio dia 10/03: o método é Integral para qualquer motivo.
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora, new DateOnly(2026, 3, 20));

        var simulacao = apolice.SimularCancelamento(motivo, Cenarios.Hoje).DeveTerSucesso();

        simulacao.ShouldBe(new SimulacaoCancelamento(Cenarios.Hoje, 0, 365, MetodoCancelamento.Integral, null, 3_800.00m, 3_800.00m));
    }

    [Fact]
    public void Deve_restituir_o_ajuste_do_ano_inteiro_quando_programada_endossada_e_cancelada_antes_do_inicio()
    {
        // Endosso em 10/03 numa apólice com início em 20/03: efeito no início, VIDROS cobrado inteiro (220 × 365/365).
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora, new DateOnly(2026, 3, 20));
        apolice.Endossar(new(TipoEndosso.InclusaoCobertura, "VIDROS"), CatalogoPadrao.Auto(), Tarifador.Padrao(), Cenarios.AnalistaId, Cenarios.Agora)
            .DeveTerSucesso();

        var simulacao = apolice.SimularCancelamento(MotivoCancelamento.PedidoDoSegurado, Cenarios.Hoje).DeveTerSucesso();

        simulacao.Metodo.ShouldBe(MetodoCancelamento.Integral);
        (simulacao.PremioLiquidoBase, simulacao.ValorRestituicao).ShouldBe((4_020.00m, 4_020.00m)); // 3.800 + 220
    }

    [Theory]
    [InlineData(0, "0.13", "3306.00")] // dia do início: 1ª linha (15 dias) → 3.800 × 0,87
    [InlineData(15, "0.13", "3306.00")] // borda da 1ª linha
    [InlineData(16, "0.20", "3040.00")] // passa para a linha de 30 dias → 3.800 × 0,80
    [InlineData(184, "0.73", "1026.00")] // 10/09/2026: linha de 195 dias → 3.800 × 0,27
    [InlineData(365, "1.00", "0.00")] // último dia de vigência: retenção total
    public void Deve_aplicar_tabela_de_prazo_curto_quando_pedido_do_segurado(int diasDecorridos, string retido, string restituicao)
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);
        var hoje = Inicio.AddDays(diasDecorridos);

        var simulacao = apolice.SimularCancelamento(MotivoCancelamento.PedidoDoSegurado, hoje).DeveTerSucesso();

        simulacao.Metodo.ShouldBe(MetodoCancelamento.PrazoCurto);
        (simulacao.DataCancelamento, simulacao.DiasVigencia, simulacao.PremioLiquidoBase).ShouldBe((hoje, 365, 3_800.00m));
        simulacao.DiasDecorridos.ShouldBe(diasDecorridos);
        simulacao.PercentualRetido.ShouldBe(Valor.De(retido));
        simulacao.ValorRestituicao.ShouldBe(Valor.De(restituicao));
    }

    [Theory]
    [InlineData(MotivoCancelamento.IniciativaSeguradora, 184, "1884.38")] // 3.800 × 181 / 365 = 1.884,3835…
    [InlineData(MotivoCancelamento.Inadimplencia, 0, "3800.00")] // 3.800 × 365 / 365
    [InlineData(MotivoCancelamento.Inadimplencia, 365, "0.00")] // último dia: nada a restituir
    public void Deve_restituir_pro_rata_die_quando_motivo_nao_e_pedido_do_segurado(MotivoCancelamento motivo, int diasDecorridos, string restituicao)
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);

        var simulacao = apolice.SimularCancelamento(motivo, Inicio.AddDays(diasDecorridos)).DeveTerSucesso();

        simulacao.Metodo.ShouldBe(MetodoCancelamento.ProRataDie);
        simulacao.PercentualRetido.ShouldBeNull();
        simulacao.ValorRestituicao.ShouldBe(Valor.De(restituicao));
    }

    [Fact]
    public void Deve_usar_366_dias_quando_vigencia_atravessa_ano_bissexto()
    {
        // Emitida em 01/03/2027 com início 10/03/2027 → fim 10/03/2028 (inclui 29/02/2028).
        var apolice = Cenarios.ApoliceAuto(Cenarios.Em(2027, 3, 1), new DateOnly(2027, 3, 10));

        var proRata = apolice.SimularCancelamento(MotivoCancelamento.IniciativaSeguradora, new DateOnly(2027, 9, 10)).DeveTerSucesso();
        var ultimoDia = apolice.SimularCancelamento(MotivoCancelamento.PedidoDoSegurado, new DateOnly(2028, 3, 10)).DeveTerSucesso();

        // Restam 182 dias (30+31+30+31+31+29): 3.800 × 182 / 366 = 1.889,6174… → 1.889,62
        proRata.DiasVigencia.ShouldBe(366);
        proRata.ValorRestituicao.ShouldBe(1_889.62m);
        // 366 dias decorridos ficam além da última linha (365) e também retêm 100%.
        (ultimoDia.DiasDecorridos, ultimoDia.PercentualRetido, ultimoDia.ValorRestituicao).ShouldBe((366, (decimal?)1.00m, 0.00m));
    }

    [Fact]
    public void Deve_partir_do_premio_vigente_quando_houve_endosso()
    {
        // Com ASSISTENCIA_24H: líquido 3.920; endosso RCF_DM → 200.000 em 10/09/2026 soma 297,53 → base 4.217,53.
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora, coberturas: Cenarios.CoberturasAutoComAssistencia());
        apolice.Endossar(new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), CatalogoPadrao.Auto(), Tarifador.Padrao(), Cenarios.AnalistaId, Cenarios.Em(2026, 9, 10));

        var simulacao = apolice.SimularCancelamento(MotivoCancelamento.PedidoDoSegurado, new DateOnly(2026, 9, 10)).DeveTerSucesso();

        // 184 dias decorridos → retém 73%: 4.217,53 × 0,27 = 1.138,7331 → 1.138,73
        simulacao.PremioLiquidoBase.ShouldBe(4_217.53m);
        simulacao.ValorRestituicao.ShouldBe(1_138.73m);
    }

    [Fact]
    public void Deve_gravar_restituicao_e_levantar_evento_quando_cancela()
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);
        apolice.ClearDomainEvents();
        var agora = Cenarios.Em(2026, 3, 26, hora: 9);

        var simulacao = apolice.Cancelar(MotivoCancelamento.PedidoDoSegurado, agora).DeveTerSucesso();

        simulacao.ValorRestituicao.ShouldBe(3_040.00m); // 16 dias → retém 20%
        apolice.Status.ShouldBe(StatusApolice.Cancelada);
        apolice.StatusEfetivo(new DateOnly(2026, 3, 26)).ShouldBe(StatusEfetivoApolice.Cancelada);
        (apolice.CanceladaEm, apolice.DataCancelamento).ShouldBe(((DateTimeOffset?)agora, (DateOnly?)new DateOnly(2026, 3, 26)));
        apolice.MotivoCancelamento.ShouldBe(MotivoCancelamento.PedidoDoSegurado);
        apolice.ValorRestituicao.ShouldBe(3_040.00m);

        var evento = apolice.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ApoliceCancelada>();
        (evento.ApoliceId, evento.Numero, evento.Motivo, evento.ValorRestituicao).ShouldBe((apolice.Id, Cenarios.NumeroApolice, MotivoCancelamento.PedidoDoSegurado, 3_040.00m));
        (evento.SeguradoNome, evento.SeguradoEmail, evento.OcorridoEm).ShouldBe(("Maria Silva", "maria.silva@exemplo.com", agora));
    }

    [Fact]
    public void Deve_contar_os_dias_no_fuso_de_brasilia_quando_cancela_as_23h30()
    {
        // 02:30Z de 26/03 = 23:30 de 25/03 em Brasília: 15 dias decorridos → retém 13% (em UTC seriam 16 → 20%).
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);

        var simulacao = apolice.Cancelar(MotivoCancelamento.PedidoDoSegurado, new DateTimeOffset(2026, 3, 26, 2, 30, 0, TimeSpan.Zero))
            .DeveTerSucesso();

        (simulacao.DataCancelamento, simulacao.DiasDecorridos, simulacao.ValorRestituicao).ShouldBe((new DateOnly(2026, 3, 25), 15, 3_306.00m));
        apolice.DataCancelamento.ShouldBe(new DateOnly(2026, 3, 25));
    }

    [Fact]
    public void Deve_negar_quando_ja_cancelada_ou_vencida()
    {
        var cancelada = Cenarios.ApoliceAuto(Cenarios.Agora);
        cancelada.Cancelar(MotivoCancelamento.Inadimplencia, Cenarios.Em(2026, 4, 1));
        var vencida = Cenarios.ApoliceAuto(Cenarios.Agora);

        cancelada.Cancelar(MotivoCancelamento.PedidoDoSegurado, Cenarios.Em(2026, 4, 2)).DeveFalharCom("Apolice.StatusInvalido", ErrorType.RegraDeNegocio);
        vencida.SimularCancelamento(MotivoCancelamento.PedidoDoSegurado, new DateOnly(2027, 3, 11)).DeveFalharCom("Apolice.StatusInvalido");
        cancelada.MotivoCancelamento.ShouldBe(MotivoCancelamento.Inadimplencia);
    }

    [Fact]
    public void Deve_rejeitar_quando_motivo_invalido()
    {
        Cenarios.ApoliceAuto(Cenarios.Agora).SimularCancelamento((MotivoCancelamento)99, Cenarios.Hoje)
            .DeveFalharCom("Apolice.MotivoCancelamentoInvalido", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_manter_a_apolice_intacta_quando_apenas_simula()
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);
        apolice.ClearDomainEvents();

        apolice.SimularCancelamento(MotivoCancelamento.PedidoDoSegurado, Cenarios.Hoje).DeveTerSucesso();

        apolice.Status.ShouldBe(StatusApolice.Ativa);
        apolice.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Deve_lancar_quando_calculadora_recebe_vigencia_nula()
    {
        Should.Throw<ArgumentNullException>(() => CalculadoraCancelamento.Simular(MotivoCancelamento.PedidoDoSegurado, null!, 1m, Cenarios.Hoje));
    }
}

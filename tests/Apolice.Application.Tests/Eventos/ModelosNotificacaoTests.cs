using Apolice.Application.Features.Eventos;
using Apolice.Domain.Apolices;

namespace Apolice.Application.Tests.Eventos;

public sealed class ModelosNotificacaoTests
{
    [Theory]
    [InlineData(150.5, "Ajuste de prêmio a pagar: R$ 150,50.")]
    [InlineData(-1234.5, "Valor a restituir: R$ 1.234,50.")]
    [InlineData(0, "Não houve alteração no valor do prêmio.")]
    public void Deve_descrever_o_ajuste_conforme_o_sinal_quando_endosso(double ajuste, string esperado)
    {
        ModelosNotificacao.DescreverAjuste((decimal)ajuste).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(TipoEndosso.AlteracaoLmi, "alteração do limite máximo de indenização (LMI)")]
    [InlineData(TipoEndosso.InclusaoCobertura, "inclusão")]
    [InlineData(TipoEndosso.ExclusaoCobertura, "exclusão")]
    [InlineData((TipoEndosso)42, "alteração")]
    public void Deve_descrever_o_tipo_em_portugues_quando_endosso(TipoEndosso tipo, string esperado)
    {
        ModelosNotificacao.DescreverTipo(tipo).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(MotivoCancelamento.PedidoDoSegurado, "pedido do segurado")]
    [InlineData(MotivoCancelamento.IniciativaSeguradora, "iniciativa da seguradora")]
    [InlineData(MotivoCancelamento.Inadimplencia, "inadimplência")]
    [InlineData((MotivoCancelamento)42, "motivo administrativo")]
    public void Deve_descrever_o_motivo_em_portugues_quando_cancelamento(MotivoCancelamento motivo, string esperado)
    {
        ModelosNotificacao.DescreverMotivo(motivo).ShouldBe(esperado);
    }

    [Fact]
    public void Deve_informar_que_nao_ha_restituicao_quando_valor_zero()
    {
        var evento = new ApoliceCancelada(Guid.NewGuid(), "AUTO-2026-000001", MotivoCancelamento.IniciativaSeguradora, 0m, "Maria Silva", "maria@exemplo.com", DateTimeOffset.UnixEpoch);

        var modelo = ModelosNotificacao.Cancelamento(evento);

        modelo.Corpo.ShouldContain("Não há valor de prêmio a restituir.");
        modelo.Corpo.ShouldContain("por iniciativa da seguradora.");
    }

    [Fact]
    public void Deve_omitir_a_data_de_efeito_quando_endosso_nao_foi_encontrado()
    {
        var evento = new ApoliceEndossada(Guid.NewGuid(), "AUTO-2026-000001", 2, TipoEndosso.ExclusaoCobertura, "VIDROS", -50m, "Maria Silva", "maria@exemplo.com", DateTimeOffset.UnixEpoch);

        var modelo = ModelosNotificacao.Endosso(evento, "VIDROS", null);

        modelo.Corpo.ShouldContain("exclusão da cobertura VIDROS.");
        modelo.Corpo.ShouldContain("Valor a restituir: R$ 50,00.");
    }
}

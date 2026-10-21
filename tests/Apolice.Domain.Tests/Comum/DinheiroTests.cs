using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Comum;

public sealed class DinheiroTests
{
    [Theory]
    [InlineData("1234.56", "R$ 1.234,56")]
    [InlineData("1234567.891", "R$ 1.234.567,89")]
    [InlineData("0", "R$ 0,00")]
    [InlineData("0.005", "R$ 0,01")] // meio centavo arredonda para cima (AwayFromZero)
    [InlineData("-1234.5", "-R$ 1.234,50")]
    public void Deve_formatar_em_pt_BR_quando_convertido_para_texto(string valor, string esperado)
    {
        new Dinheiro(Valor.De(valor)).ToString().ShouldBe(esperado);
    }

    [Theory]
    [InlineData("2.345", "2.35")] // 2,345 → meio centavo sobe
    [InlineData("2.344", "2.34")]
    [InlineData("-2.345", "-2.35")] // AwayFromZero também no negativo
    [InlineData("2.5", "2.5")]
    public void Deve_arredondar_meio_centavo_para_longe_do_zero_quando_arredonda(string valor, string esperado)
    {
        var entrada = Valor.De(valor);
        var saida = Valor.De(esperado);

        Dinheiro.Arredondar(entrada).ShouldBe(saida);
        new Dinheiro(entrada).Arredondado().ShouldBe(new Dinheiro(saida));
    }

    [Theory]
    [InlineData("100000.12", true)]
    [InlineData("100000.120", true)] // escala 3, mas centavos exatos
    [InlineData("100000", true)]
    [InlineData("100000.005", false)]
    [InlineData("-0.001", false)]
    public void Deve_indicar_centavos_exatos_quando_verifica_a_escala(string valor, bool esperado)
    {
        Dinheiro.EstaEmCentavos(Valor.De(valor)).ShouldBe(esperado);
    }

    [Fact]
    public void Deve_truncar_centavos_quando_arredonda_para_baixo()
    {
        Dinheiro.ArredondarParaBaixo(2.349m).ShouldBe(2.34m);
        Dinheiro.ArredondarParaBaixo(408.044m).ShouldBe(408.04m);
    }

    [Fact]
    public void Deve_somar_subtrair_e_multiplicar_quando_usa_operadores()
    {
        var dez = new Dinheiro(10m);

        (dez + new Dinheiro(5.5m)).ShouldBe(new Dinheiro(15.5m));
        (dez - new Dinheiro(2.25m)).ShouldBe(new Dinheiro(7.75m));
        (-dez).ShouldBe(new Dinheiro(-10m));
        (dez * 1.5m).ShouldBe(new Dinheiro(15m));
        (1.5m * dez).ShouldBe(new Dinheiro(15m));
        Dinheiro.Zero.ShouldBe(new Dinheiro(0m));
    }

    [Fact]
    public void Deve_comparar_pelo_valor_quando_usa_operadores_relacionais()
    {
        var menor = new Dinheiro(1m);
        var maior = new Dinheiro(2m);

        (menor < maior).ShouldBeTrue();
        (maior > menor).ShouldBeTrue();
        (menor <= new Dinheiro(1m)).ShouldBeTrue();
        (maior >= new Dinheiro(2m)).ShouldBeTrue();
        (maior < menor).ShouldBeFalse();
        menor.CompareTo(maior).ShouldBeLessThan(0);
    }
}

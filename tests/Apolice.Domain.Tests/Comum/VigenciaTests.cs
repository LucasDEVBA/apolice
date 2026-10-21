using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Comum;

public sealed class VigenciaTests
{
    private static readonly DateOnly Inicio = new(2026, 3, 10);
    private static readonly Vigencia Anual = Vigencia.Anual(Inicio);

    [Fact]
    public void Deve_durar_um_ano_quando_anual()
    {
        Anual.Fim.ShouldBe(new DateOnly(2027, 3, 10));
        Anual.DiasTotais.ShouldBe(365);
    }

    [Fact]
    public void Deve_ter_366_dias_quando_atravessa_29_de_fevereiro()
    {
        // 10/03/2027 → 10/03/2028 inclui 29/02/2028.
        Vigencia.Anual(new DateOnly(2027, 3, 10)).DiasTotais.ShouldBe(366);
    }

    [Fact]
    public void Deve_terminar_em_28_de_fevereiro_quando_inicia_em_29_de_fevereiro()
    {
        var vigencia = Vigencia.Anual(new DateOnly(2028, 2, 29));

        vigencia.Fim.ShouldBe(new DateOnly(2029, 2, 28));
        vigencia.DiasTotais.ShouldBe(365);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deve_rejeitar_quando_fim_nao_e_posterior_ao_inicio(int diasAteFim)
    {
        Vigencia.Criar(Inicio, Inicio.AddDays(diasAteFim)).DeveFalharCom("Vigencia.Invalida", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_criar_quando_fim_posterior_ao_inicio()
    {
        Vigencia.Criar(Inicio, Inicio.AddDays(1)).DeveTerSucesso().DiasTotais.ShouldBe(1);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)] // primeiro dia
    [InlineData(365, true)] // último dia ainda coberto
    [InlineData(366, false)]
    public void Deve_conter_a_data_quando_entre_inicio_e_fim_inclusivos(int diasAposInicio, bool esperado)
    {
        Anual.Contem(Inicio.AddDays(diasAposInicio)).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(-10, 0, 365)] // antes do início: limitado a 0 decorridos
    [InlineData(0, 0, 365)]
    [InlineData(15, 15, 350)]
    [InlineData(365, 365, 0)]
    [InlineData(400, 365, 0)] // após o fim: limitado à vigência
    public void Deve_limitar_a_vigencia_quando_calcula_dias_decorridos_e_restantes(int diasAposInicio, int decorridos, int restantes)
    {
        var data = Inicio.AddDays(diasAposInicio);

        Anual.DiasDecorridos(data).ShouldBe(decorridos);
        Anual.DiasRestantes(data).ShouldBe(restantes);
    }

    [Fact]
    public void Deve_reconstituir_quando_valor_persistido_valido()
    {
        Vigencia.Reconstituir(Inicio, Inicio.AddYears(1)).ShouldBe(Anual);
    }

    [Fact]
    public void Deve_lancar_quando_valor_persistido_invertido()
    {
        Should.Throw<DadoPersistidoInvalidoException>(() => Vigencia.Reconstituir(Inicio, Inicio));
    }
}

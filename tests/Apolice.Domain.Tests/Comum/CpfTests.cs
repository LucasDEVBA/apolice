using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Comum;

public sealed class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData("111 444 777 35", "11144477735")]
    [InlineData("123.456.789-09", "12345678909")] // 1º DV com resto < 2 vira 0
    public void Deve_guardar_so_digitos_quando_cpf_valido(string entrada, string esperado)
    {
        Cpf.Criar(entrada).DeveTerSucesso().Numero.ShouldBe(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("111.111.111-11")] // sequência repetida passa nos DVs, mas é rejeitada
    [InlineData("00000000000")]
    [InlineData("529.982.247-15")] // 1º DV errado
    [InlineData("529.982.247-26")] // 2º DV errado
    [InlineData("5299822472")] // 10 dígitos
    [InlineData("529982247250")] // 12 dígitos
    [InlineData("529.982.247-2a")]
    [InlineData("529/982/247-25")]
    public void Deve_rejeitar_quando_cpf_invalido(string? entrada)
    {
        Cpf.Criar(entrada).DeveFalharCom("Cpf.Invalido", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_mascarar_quando_exibido()
    {
        var cpf = Cpf.Criar("123.456.789-09").Value;

        cpf.Mascarado.ShouldBe("***.456.789-**");
        cpf.Formatado.ShouldBe("123.456.789-09");
        cpf.ToString().ShouldBe("***.456.789-**");
    }

    [Fact]
    public void Deve_ser_igual_quando_mesmo_numero_em_formatos_diferentes()
    {
        Cpf.Criar("529.982.247-25").Value.ShouldBe(Cpf.Criar("52998224725").Value);
    }

    [Fact]
    public void Deve_reconstituir_quando_valor_persistido_valido()
    {
        Cpf.Reconstituir("52998224725").Numero.ShouldBe("52998224725");
    }

    [Theory]
    [InlineData("529.982.247-25")] // gravado com máscara = fora do formato só-dígitos
    [InlineData("11111111111")]
    [InlineData("52998224726")]
    public void Deve_lancar_quando_valor_persistido_corrompido(string persistido)
    {
        Should.Throw<DadoPersistidoInvalidoException>(() => Cpf.Reconstituir(persistido));
    }
}

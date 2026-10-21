using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Comum;

public sealed class EmailTests
{
    private const string Dominio = "@exemplo.com";

    [Fact]
    public void Deve_normalizar_com_trim_e_minusculas_quando_email_valido()
    {
        var email = Email.Criar("  Maria.Silva@Exemplo.COM ").DeveTerSucesso();

        email.Valor.ShouldBe("maria.silva@exemplo.com");
        email.ToString().ShouldBe("maria.silva@exemplo.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("maria")]
    [InlineData("maria@")]
    [InlineData("@exemplo.com")]
    [InlineData("maria@exemplo")]
    [InlineData("ma ria@exemplo.com")]
    [InlineData("a@b@exemplo.com")]
    public void Deve_rejeitar_quando_formato_invalido(string? entrada)
    {
        Email.Criar(entrada).DeveFalharCom("Email.Invalido", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_quando_email_passa_de_254_caracteres()
    {
        // 242 + 12 ("@exemplo.com") = 254 (limite); 243 + 12 = 255.
        Email.Criar(new string('a', 242) + Dominio).IsSuccess.ShouldBeTrue();
        Email.Criar(new string('a', 243) + Dominio).DeveFalharCom("Email.Invalido");
    }

    [Fact]
    public void Deve_reconstituir_quando_valor_persistido_normalizado()
    {
        Email.Reconstituir("maria@exemplo.com").Valor.ShouldBe("maria@exemplo.com");
    }

    [Theory]
    [InlineData("Maria@Exemplo.com")] // não normalizado = gravado por fora do domínio
    [InlineData("invalido")]
    public void Deve_lancar_quando_valor_persistido_corrompido(string persistido)
    {
        Should.Throw<DadoPersistidoInvalidoException>(() => Email.Reconstituir(persistido));
    }
}

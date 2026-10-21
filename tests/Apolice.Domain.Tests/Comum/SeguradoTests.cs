using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Comum;

public sealed class SeguradoTests
{
    private const string CpfValido = "529.982.247-25";
    private const string EmailValido = "maria@exemplo.com";
    private static readonly DateOnly Nascimento = new(1990, 3, 11);

    [Fact]
    public void Deve_normalizar_nome_cpf_e_email_quando_dados_validos()
    {
        var segurado = Segurado.Criar("  Maria Silva  ", CpfValido, " Maria@Exemplo.com ", Nascimento).DeveTerSucesso();

        segurado.Nome.ShouldBe("Maria Silva");
        segurado.Cpf.Numero.ShouldBe("52998224725");
        segurado.Email.Valor.ShouldBe("maria@exemplo.com");
        segurado.DataNascimento.ShouldBe(Nascimento);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ma")]
    public void Deve_rejeitar_quando_nome_curto_ou_ausente(string? nome)
    {
        Segurado.Criar(nome, CpfValido, EmailValido, Nascimento).DeveFalharCom("Segurado.NomeInvalido", ErrorType.Validacao);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void Deve_validar_quando_nome_nas_bordas_de_3_a_120(int tamanho, bool valido)
    {
        Segurado.Criar(new string('a', tamanho), CpfValido, EmailValido, Nascimento).IsSuccess.ShouldBe(valido);
    }

    [Fact]
    public void Deve_propagar_erro_do_cpf_ou_do_email_quando_invalidos()
    {
        Segurado.Criar("Maria Silva", "111.111.111-11", EmailValido, Nascimento).DeveFalharCom("Cpf.Invalido");
        Segurado.Criar("Maria Silva", CpfValido, "maria", Nascimento).DeveFalharCom("Email.Invalido");
    }

    [Fact]
    public void Deve_rejeitar_quando_nascimento_anterior_a_1900()
    {
        Segurado.Criar("Maria Silva", CpfValido, EmailValido, new DateOnly(1899, 12, 31))
            .DeveFalharCom("Segurado.DataNascimentoInvalida");
    }

    [Fact]
    public void Deve_lancar_quando_vo_nulo_e_passado_direto()
    {
        var email = Email.Criar(EmailValido).Value;

        Should.Throw<ArgumentNullException>(() => Segurado.Criar("Maria Silva", (Cpf)null!, email, Nascimento));
    }

    [Theory]
    [InlineData(2026, 3, 10, 35)] // véspera do aniversário: 2026 − 1990 − 1
    [InlineData(2026, 3, 11, 36)] // no aniversário
    public void Deve_calcular_idade_completa_quando_consulta_em_uma_data(int ano, int mes, int dia, int idade)
    {
        Cenarios.SeguradoPadrao(Nascimento).IdadeEm(new DateOnly(ano, mes, dia)).ShouldBe(idade);
    }

    [Theory]
    [InlineData(27, 25)]
    [InlineData(28, 26)] // ano não bissexto: aniversário de quem nasceu em 29/02 conta em 28/02
    public void Deve_tratar_nascido_em_29_de_fevereiro_quando_ano_nao_bissexto(int diaFevereiro, int idade)
    {
        Cenarios.SeguradoPadrao(new DateOnly(2000, 2, 29)).IdadeEm(new DateOnly(2026, 2, diaFevereiro)).ShouldBe(idade);
    }

    [Fact]
    public void Deve_ser_igual_quando_mesmos_dados()
    {
        Cenarios.SeguradoPadrao().ShouldBe(Cenarios.SeguradoPadrao());
    }

    [Fact]
    public void Deve_reconstituir_quando_valores_persistidos_validos()
    {
        var segurado = Segurado.Reconstituir("Maria Silva", "52998224725", "maria.silva@exemplo.com", new DateOnly(1985, 5, 20));

        segurado.ShouldBe(Cenarios.SeguradoPadrao());
    }

    [Fact]
    public void Deve_lancar_quando_valores_persistidos_corrompidos()
    {
        Should.Throw<DadoPersistidoInvalidoException>(() =>
            Segurado.Reconstituir("Ma", "52998224725", EmailValido, Nascimento));
        Should.Throw<DadoPersistidoInvalidoException>(() =>
            Segurado.Reconstituir("Maria Silva", CpfValido, EmailValido, Nascimento));
    }
}

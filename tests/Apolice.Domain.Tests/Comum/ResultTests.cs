using System.Reflection;
using Apolice.Domain.Comum;

namespace Apolice.Domain.Tests.Comum;

public sealed class ResultTests
{
    private static readonly Error ErroQualquer = Error.Validacao("Teste.Erro", "Erro de teste.");

    [Fact]
    public void Deve_indicar_sucesso_quando_criado_com_Success()
    {
        var resultado = Result.Success();

        resultado.IsSuccess.ShouldBeTrue();
        resultado.IsFailure.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => resultado.Error);
    }

    [Fact]
    public void Deve_expor_o_erro_quando_criado_com_Failure()
    {
        var resultado = Result.Failure(ErroQualquer);

        resultado.IsFailure.ShouldBeTrue();
        resultado.Error.ShouldBe(ErroQualquer);
    }

    [Fact]
    public void Deve_lancar_ao_ler_Value_quando_resultado_falhou()
    {
        var resultado = Result.Failure<int>(ErroQualquer);

        var excecao = Should.Throw<InvalidOperationException>(() => resultado.Value);
        excecao.Message.ShouldContain("Teste.Erro");
    }

    [Fact]
    public void Deve_converter_implicitamente_quando_recebe_valor_ou_erro()
    {
        Result<int> sucesso = 42;
        Result<int> falhaGenerica = ErroQualquer;
        Result falha = ErroQualquer;

        sucesso.Value.ShouldBe(42);
        Result.Success(7).Value.ShouldBe(7);
        falhaGenerica.Error.ShouldBe(ErroQualquer);
        falha.Error.ShouldBe(ErroQualquer);
    }

    [Fact]
    public void Deve_lancar_quando_falha_sem_erro_ou_sucesso_sem_valor()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
        Should.Throw<ArgumentNullException>(() => Result<string>.Failure(null!));
        Should.Throw<ArgumentNullException>(() => Result<string>.Success(null!));
    }

    [Fact]
    public void Deve_restringir_heranca_ao_dominio_quando_construtor_e_private_protected()
    {
        var construtores = typeof(Result).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        construtores.ShouldHaveSingleItem().IsFamilyAndAssembly.ShouldBeTrue();
    }

    [Fact]
    public void Deve_definir_o_tipo_quando_usa_cada_fabrica_de_erro()
    {
        Error.Validacao("a", "b").Tipo.ShouldBe(ErrorType.Validacao);
        Error.NaoEncontrado("a", "b").Tipo.ShouldBe(ErrorType.NaoEncontrado);
        Error.Conflito("a", "b").Tipo.ShouldBe(ErrorType.Conflito);
        Error.Proibido("a", "b").Tipo.ShouldBe(ErrorType.Proibido);
        Error.RegraDeNegocio("a", "b").Tipo.ShouldBe(ErrorType.RegraDeNegocio);
        Error.NaoAutorizado("a", "b").Tipo.ShouldBe(ErrorType.NaoAutorizado);
    }
}

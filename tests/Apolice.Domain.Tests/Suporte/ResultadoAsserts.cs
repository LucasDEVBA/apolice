using Apolice.Domain.Comum;

namespace Apolice.Domain.Tests.Suporte;

internal static class ResultadoAsserts
{
    public static void DeveFalharCom(this Result resultado, string codigo, ErrorType? tipo = null)
    {
        resultado.IsFailure.ShouldBeTrue($"Esperava falha '{codigo}', mas o resultado foi sucesso.");
        resultado.Error.Codigo.ShouldBe(codigo);
        if (tipo is not null)
        {
            resultado.Error.Tipo.ShouldBe(tipo.Value);
        }
    }

    public static T DeveTerSucesso<T>(this Result<T> resultado)
    {
        resultado.IsSuccess.ShouldBeTrue(resultado.IsFailure ? $"Falhou com '{resultado.Error.Codigo}': {resultado.Error.Mensagem}" : null);
        return resultado.Value;
    }

    public static void DeveTerSucesso(this Result resultado) =>
        resultado.IsSuccess.ShouldBeTrue(resultado.IsFailure ? $"Falhou com '{resultado.Error.Codigo}': {resultado.Error.Mensagem}" : null);
}

using System.Globalization;

namespace Apolice.Application.Abstractions.Data;

// Chaves canônicas do numerador (SPEC 6): uma sequência por produto e ano para apólices e uma por ano para sinistros.
public static class ChavesNumerador
{
    public static string Apolice(string produtoCodigo, int ano) =>
        string.Create(CultureInfo.InvariantCulture, $"APOLICE:{produtoCodigo}:{ano}");

    public static string Sinistro(int ano) =>
        string.Create(CultureInfo.InvariantCulture, $"SINISTRO:{ano}");
}

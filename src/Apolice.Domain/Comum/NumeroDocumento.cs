using System.Globalization;

namespace Apolice.Domain.Comum;

// Formato canônico PREFIXO-AAAA-NNNNNN, comum a apólice e sinistro. O sequencial vem do numerador da
// infraestrutura; o domínio recusa outro formato para não persistir um número que busca e PDF não reconhecem.
internal static class NumeroDocumento
{
    private const char Separador = '-';
    private const int QuantidadePartes = 3;
    private const int DigitosAno = 4;
    private const int DigitosSequencialMinimo = 6;

    public static string Formatar(string prefixo, int ano, long sequencial) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{prefixo}{Separador}{ano}{Separador}{sequencial.ToString($"D{DigitosSequencialMinimo}", CultureInfo.InvariantCulture)}");

    public static bool EhCanonico(string numero, string prefixo)
    {
        var partes = numero.Split(Separador);
        return partes.Length == QuantidadePartes
            && partes[0] == prefixo
            && partes[1].Length == DigitosAno && partes[1].All(char.IsAsciiDigit)
            && partes[2].Length >= DigitosSequencialMinimo && partes[2].All(char.IsAsciiDigit);
    }
}

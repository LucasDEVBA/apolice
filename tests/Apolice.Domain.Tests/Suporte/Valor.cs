using System.Globalization;

namespace Apolice.Domain.Tests.Suporte;

// Atributos não aceitam decimal: os casos de [InlineData] trazem o valor como texto invariante.
internal static class Valor
{
    public static decimal De(string texto) => decimal.Parse(texto, CultureInfo.InvariantCulture);
}

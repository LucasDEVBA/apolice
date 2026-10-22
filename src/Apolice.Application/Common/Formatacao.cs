using System.Globalization;
using Apolice.Domain.Comum;

namespace Apolice.Application.Common;

// pt-BR montado à mão (sem ICU, como o Dinheiro do domínio): o Lambda roda com globalização invariante.
internal static class Formatacao
{
    public static string Moeda(decimal valor) => Dinheiro.Formatar(valor);

    public static string Data(DateOnly data) => data.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);
}

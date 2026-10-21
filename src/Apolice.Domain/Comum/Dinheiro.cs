using System.Globalization;

namespace Apolice.Domain.Comum;

// As entidades guardam decimal (contrato do SPEC); este tipo é o ponto único das regras de arredondamento,
// de escala e de formatação, e o VO em si serve às contas e à apresentação na borda.
public readonly record struct Dinheiro(decimal Valor) : IComparable<Dinheiro>
{
    private const int CasasDecimais = 2;
    private const int TamanhoGrupoMilhar = 3;
    private const int PadraoPositivoSimboloEspacoValor = 2;
    private const int PadraoNegativoSinalSimboloEspacoValor = 9;

    // Montado à mão para não depender de ICU (o Lambda roda com globalização invariante).
    private static readonly NumberFormatInfo FormatoBrasileiro = CriarFormatoBrasileiro();

    public static Dinheiro Zero { get; } = new(0m);

    public static decimal Arredondar(decimal valor) =>
        Math.Round(valor, CasasDecimais, MidpointRounding.AwayFromZero);

    public static decimal ArredondarParaBaixo(decimal valor) =>
        Math.Round(valor, CasasDecimais, MidpointRounding.ToNegativeInfinity);

    // Igualdade de decimal ignora a escala: 10,120 conta como centavos exatos; 10,125 não.
    public static bool EstaEmCentavos(decimal valor) => valor == Arredondar(valor);

    public static string Formatar(decimal valor) => Arredondar(valor).ToString("C", FormatoBrasileiro);

    public Dinheiro Arredondado() => new(Arredondar(Valor));

    public int CompareTo(Dinheiro other) => Valor.CompareTo(other.Valor);

    public override string ToString() => Formatar(Valor);

    public static Dinheiro operator +(Dinheiro esquerda, Dinheiro direita) => new(esquerda.Valor + direita.Valor);

    public static Dinheiro operator -(Dinheiro esquerda, Dinheiro direita) => new(esquerda.Valor - direita.Valor);

    public static Dinheiro operator -(Dinheiro valor) => new(-valor.Valor);

    public static Dinheiro operator *(Dinheiro valor, decimal fator) => new(valor.Valor * fator);

    public static Dinheiro operator *(decimal fator, Dinheiro valor) => new(valor.Valor * fator);

    public static bool operator <(Dinheiro esquerda, Dinheiro direita) => esquerda.CompareTo(direita) < 0;

    public static bool operator >(Dinheiro esquerda, Dinheiro direita) => esquerda.CompareTo(direita) > 0;

    public static bool operator <=(Dinheiro esquerda, Dinheiro direita) => esquerda.CompareTo(direita) <= 0;

    public static bool operator >=(Dinheiro esquerda, Dinheiro direita) => esquerda.CompareTo(direita) >= 0;

    private static NumberFormatInfo CriarFormatoBrasileiro()
    {
        var formato = new NumberFormatInfo
        {
            CurrencySymbol = "R$",
            CurrencyDecimalSeparator = ",",
            CurrencyGroupSeparator = ".",
            CurrencyDecimalDigits = CasasDecimais,
            CurrencyGroupSizes = [TamanhoGrupoMilhar],
            CurrencyPositivePattern = PadraoPositivoSimboloEspacoValor,
            CurrencyNegativePattern = PadraoNegativoSinalSimboloEspacoValor,
            NegativeSign = "-",
        };
        return NumberFormatInfo.ReadOnly(formato);
    }
}

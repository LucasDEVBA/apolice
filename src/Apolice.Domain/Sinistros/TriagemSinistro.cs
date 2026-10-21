using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public sealed record TriagemSinistro
{
    public const int ScoreMinimo = 0;
    public const int ScoreMaximo = 100;

    // Exigido pelo EF Core; as propriedades são preenchidas pelo materializador.
    private TriagemSinistro()
    {
        Alertas = [];
    }

    private TriagemSinistro(int score, IReadOnlyList<string> alertas, DateTimeOffset triadoEm)
    {
        Score = score;
        Alertas = alertas;
        TriadoEm = triadoEm;
    }

    public int Score { get; private init; }

    public IReadOnlyList<string> Alertas { get; private init; }

    public DateTimeOffset TriadoEm { get; private init; }

    public static Result<TriagemSinistro> Criar(int score, IReadOnlyList<string>? alertas, DateTimeOffset triadoEm)
    {
        if (score is < ScoreMinimo or > ScoreMaximo || alertas is null || alertas.Any(string.IsNullOrWhiteSpace))
        {
            return SinistroErros.TriagemInvalida(ScoreMinimo, ScoreMaximo);
        }

        // Cópia defensiva: quem montou a lista não consegue alterar a triagem depois de registrada.
        return new TriagemSinistro(score, [.. alertas], triadoEm);
    }

    // Para leituras fora do EF; o EF materializa o tipo complexo pelos init privados, sem passar por aqui.
    public static TriagemSinistro Reconstituir(int score, IReadOnlyList<string> alertas, DateTimeOffset triadoEm)
    {
        var triagem = Criar(score, alertas, triadoEm);
        return triagem.IsSuccess
            ? triagem.Value
            : throw new DadoPersistidoInvalidoException("Triagem de sinistro persistida inválida.");
    }

    // Igualdade de VO pelo conteúdo dos alertas: o record compararia a referência da lista.
    public bool Equals(TriagemSinistro? other) =>
        other is not null
        && Score == other.Score
        && TriadoEm == other.TriadoEm
        && Alertas.SequenceEqual(other.Alertas, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Score);
        hash.Add(TriadoEm);
        foreach (var alerta in Alertas)
        {
            hash.Add(alerta, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    internal TriagemSinistro RegistradaEm(DateTimeOffset instante) => new(Score, Alertas, instante);
}

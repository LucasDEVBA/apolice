namespace Apolice.Domain.Apolices;

public static class TabelaPrazoCurto
{
    private const decimal RetencaoTotal = 1.00m;

    public static IReadOnlyList<LinhaPrazoCurto> Linhas { get; } =
    [
        new(15, 0.13m), new(30, 0.20m), new(45, 0.27m), new(60, 0.30m), new(75, 0.37m), new(90, 0.40m),
        new(105, 0.46m), new(120, 0.50m), new(135, 0.56m), new(150, 0.60m), new(165, 0.66m), new(180, 0.70m),
        new(195, 0.73m), new(210, 0.75m), new(225, 0.78m), new(240, 0.80m), new(255, 0.83m), new(270, 0.85m),
        new(285, 0.88m), new(300, 0.90m), new(315, 0.93m), new(330, 0.95m), new(345, 0.98m), new(365, RetencaoTotal),
    ];

    public static decimal PercentualRetido(int diasDecorridos)
    {
        var linha = Linhas.FirstOrDefault(l => l.Dias >= diasDecorridos);

        // Vigência em ano bissexto tem 366 dias: além da última linha a retenção já é total.
        return linha?.PercentualRetido ?? RetencaoTotal;
    }
}

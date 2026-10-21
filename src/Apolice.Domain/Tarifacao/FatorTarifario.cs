namespace Apolice.Domain.Tarifacao;

// CoberturasAlvo nulo = vale para todo item calculado por taxa (itens de prêmio fixo nunca recebem fator).
public sealed record FatorTarifario(string Descricao, decimal Fator, IReadOnlyCollection<string>? CoberturasAlvo = null)
{
    // Cópia somente leitura: as estratégias passam arrays estáticos, que não podem vazar mutáveis.
    public IReadOnlyCollection<string>? CoberturasAlvo { get; } = CoberturasAlvo is null ? null : [.. CoberturasAlvo];

    public bool AplicaA(string coberturaCodigo) => CoberturasAlvo is null || CoberturasAlvo.Contains(coberturaCodigo);

    // Igualdade de VO pelo conteúdo dos alvos (o record compararia a referência da coleção).
    public bool Equals(FatorTarifario? other) =>
        other is not null
        && Descricao == other.Descricao
        && Fator == other.Fator
        && MesmosAlvos(CoberturasAlvo, other.CoberturasAlvo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Descricao, StringComparer.Ordinal);
        hash.Add(Fator);
        foreach (var alvo in CoberturasAlvo ?? [])
        {
            hash.Add(alvo, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static bool MesmosAlvos(IReadOnlyCollection<string>? esquerda, IReadOnlyCollection<string>? direita) =>
        esquerda is null || direita is null
            ? esquerda is null && direita is null
            : esquerda.SequenceEqual(direita, StringComparer.Ordinal);
}

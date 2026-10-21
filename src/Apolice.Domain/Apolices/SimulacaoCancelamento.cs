namespace Apolice.Domain.Apolices;

// PercentualRetido é fração (0,13 = 13%), como a AliquotaIof; só existe no método PrazoCurto.
public sealed record SimulacaoCancelamento(
    DateOnly DataCancelamento,
    int DiasDecorridos,
    int DiasVigencia,
    MetodoCancelamento Metodo,
    decimal? PercentualRetido,
    decimal PremioLiquidoBase,
    decimal ValorRestituicao);

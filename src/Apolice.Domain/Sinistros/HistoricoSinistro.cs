namespace Apolice.Domain.Sinistros;

public sealed record HistoricoSinistro(
    StatusSinistro Status,
    string? Observacao,
    Guid? UsuarioId,
    DateTimeOffset OcorridoEm);

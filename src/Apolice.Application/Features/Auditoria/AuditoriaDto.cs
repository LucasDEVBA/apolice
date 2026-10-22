namespace Apolice.Application.Features.Auditoria;

public sealed record AuditoriaDto(
    Guid Id,
    string? UsuarioNome,
    string Acao,
    string Entidade,
    string? EntidadeId,
    string? Detalhes,
    string? CorrelationId,
    DateTimeOffset OcorridoEm);

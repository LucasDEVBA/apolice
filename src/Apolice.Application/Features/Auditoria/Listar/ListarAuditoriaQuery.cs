namespace Apolice.Application.Features.Auditoria.Listar;

// GET /admin/auditoria?cursor&limite.
public sealed record ListarAuditoriaQuery(string? Cursor = null, int? Limite = null);

using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Sinistros.Listar;

// GET /sinistros?status&busca&cursor&limite.
public sealed record ListarSinistrosQuery(
    StatusSinistro? Status = null,
    string? Busca = null,
    string? Cursor = null,
    int? Limite = null);

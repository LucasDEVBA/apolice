using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.Listar;

// GET /apolices?status&produto&busca&cursor&limite — status é o efetivo (Programada/Ativa/Vencida/Cancelada).
public sealed record ListarApolicesQuery(
    StatusEfetivoApolice? Status = null,
    string? Produto = null,
    string? Busca = null,
    string? Cursor = null,
    int? Limite = null);

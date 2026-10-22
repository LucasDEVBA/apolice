using Apolice.Domain.Cotacoes;

namespace Apolice.Application.Features.Cotacoes.Listar;

// GET /cotacoes?status&produto&busca&cursor&limite (a API binda com [AsParameters]).
public sealed record ListarCotacoesQuery(
    StatusEfetivoCotacao? Status = null,
    string? Produto = null,
    string? Busca = null,
    string? Cursor = null,
    int? Limite = null);

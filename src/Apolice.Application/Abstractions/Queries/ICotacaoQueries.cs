using Apolice.Application.Common;
using Apolice.Application.Features.Cotacoes;
using Apolice.Domain.Cotacoes;

namespace Apolice.Application.Abstractions.Queries;

// Lado de leitura (Dapper na infra). corretorId nulo = sem filtro (Analista/Admin); preenchido = só a carteira
// dele, e um id de outra carteira devolve null (o handler responde 404, sem revelar a existência). O handler confere
// o CorretorId do que voltar, mas o filtro é obrigatório no SQL e precisa de teste de integração próprio.
// "agora" decide o status efetivo: a expiração da cotação é por instante (ValidaAte), não por data.
public interface ICotacaoQueries
{
    Task<Pagina<CotacaoResumoDto>> ListarAsync(FiltroCotacoes filtro, CancellationToken cancellationToken);

    Task<CotacaoDto?> ObterAsync(Guid id, Guid? corretorId, DateTimeOffset agora, CancellationToken cancellationToken);
}

// Status é o EFETIVO, pela regra de Cotacao.StatusEfetivo: Expirada = Calculada/Aceita com Agora > ValidaAte.
// Busca: nome do segurado ou CPF. Keyset por (criada_em, id) DESC — Pagina.Montar com Limite + 1 linhas.
public sealed record FiltroCotacoes(
    Guid? CorretorId,
    StatusEfetivoCotacao? Status,
    string? ProdutoCodigo,
    string? Busca,
    PosicaoCursor? Apos,
    int Limite,
    DateTimeOffset Agora);

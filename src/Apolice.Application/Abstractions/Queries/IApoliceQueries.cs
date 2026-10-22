using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Domain.Apolices;

namespace Apolice.Application.Abstractions.Queries;

// corretorId nulo = sem filtro; preenchido = só a carteira dele (id alheio → null). O handler ainda confere o
// CorretorId do que voltar (defesa em profundidade), mas o filtro é obrigatório no SQL e precisa de teste de
// integração próprio. O detalhe deve sair de ApoliceMapeamento.ParaDto (cronograma e status pelas regras do
// domínio) com sinistros resumidos, notificações da apólice e dos seus sinistros, e nomes — troca consciente:
// o detalhe materializa o agregado (EF, sem tracking) em vez de copiar essas regras para o SQL.
public interface IApoliceQueries
{
    Task<Pagina<ApoliceResumoDto>> ListarAsync(FiltroApolices filtro, CancellationToken cancellationToken);

    Task<ApoliceDto?> ObterAsync(Guid id, Guid? corretorId, DateOnly hoje, CancellationToken cancellationToken);
}

// Status é o EFETIVO em Hoje, pela mesma regra de ApoliceSeguro.StatusEfetivo (a listagem precisa dela no SQL para
// filtrar): Cancelada se status = Cancelada; senão Programada se Hoje < início; Vencida se Hoje > fim; senão Ativa.
// Busca: número da apólice, nome ou CPF do segurado. Keyset por (emitida_em, id) DESC — Pagina.Montar com Limite + 1.
public sealed record FiltroApolices(
    Guid? CorretorId,
    StatusEfetivoApolice? Status,
    string? ProdutoCodigo,
    string? Busca,
    PosicaoCursor? Apos,
    int Limite,
    DateOnly Hoje);

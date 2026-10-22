using Apolice.Application.Common;
using Apolice.Application.Features.Sinistros;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Abstractions.Queries;

// corretorId nulo = sem filtro; preenchido = só os sinistros da carteira dele (id alheio → null). O SinistroDto do
// SPEC não traz o CorretorId, então aqui o filtro do SQL é a única barreira: teste de integração de escopo obrigatório.
// O detalhe traz o histórico com o nome de cada usuário (SinistroMapeamento.ParaDto resolve a forma).
public interface ISinistroQueries
{
    Task<Pagina<SinistroResumoDto>> ListarAsync(FiltroSinistros filtro, CancellationToken cancellationToken);

    Task<SinistroDto?> ObterAsync(Guid id, Guid? corretorId, CancellationToken cancellationToken);
}

// Status é o gravado. Busca: número do sinistro, número da apólice ou nome do segurado. Keyset por (aberto_em, id) DESC.
public sealed record FiltroSinistros(
    Guid? CorretorId,
    StatusSinistro? Status,
    string? Busca,
    PosicaoCursor? Apos,
    int Limite);

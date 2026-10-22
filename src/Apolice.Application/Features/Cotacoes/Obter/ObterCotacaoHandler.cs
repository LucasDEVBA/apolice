using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Cotacoes.Obter;

public sealed class ObterCotacaoHandler(ICotacaoQueries consultas, ICurrentUser usuario, TimeProvider tempo)
    : IQueryHandler<ObterCotacaoQuery, CotacaoDto>
{
    public async Task<Result<CotacaoDto>> HandleAsync(ObterCotacaoQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        // A carteira é conferida também sobre o que voltou: um filtro esquecido no SQL não vira BOLA.
        var cotacao = await consultas.ObterAsync(query.CotacaoId, escopo.Value.CorretorId, tempo.GetUtcNow(), cancellationToken);
        return cotacao is not null && escopo.Value.Alcanca(cotacao.CorretorId) ? cotacao : ComumErros.NaoEncontrado(Entidades.Cotacao);
    }
}

using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Cotacoes.Recusar;

public sealed partial class RecusarCotacaoHandler(TransicaoCotacao transicao, ILogger<RecusarCotacaoHandler> logger)
    : ICommandHandler<RecusarCotacaoCommand, CotacaoDto>
{
    public async Task<Result<CotacaoDto>> HandleAsync(RecusarCotacaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resultado = await transicao.ExecutarAsync(
            command.CotacaoId,
            (cotacao, agora) => cotacao.Recusar(agora),
            AcoesAuditoria.CotacaoRecusada,
            cancellationToken);
        if (resultado.IsSuccess)
        {
            LogRecusada(logger, command.CotacaoId);
        }

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cotação {CotacaoId} recusada.")]
    private static partial void LogRecusada(ILogger logger, Guid cotacaoId);
}

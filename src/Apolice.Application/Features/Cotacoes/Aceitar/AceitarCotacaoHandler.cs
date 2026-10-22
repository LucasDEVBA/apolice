using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Cotacoes.Aceitar;

public sealed partial class AceitarCotacaoHandler(TransicaoCotacao transicao, ILogger<AceitarCotacaoHandler> logger)
    : ICommandHandler<AceitarCotacaoCommand, CotacaoDto>
{
    public async Task<Result<CotacaoDto>> HandleAsync(AceitarCotacaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resultado = await transicao.ExecutarAsync(
            command.CotacaoId,
            (cotacao, agora) => cotacao.Aceitar(agora),
            AcoesAuditoria.CotacaoAceita,
            cancellationToken);
        if (resultado.IsSuccess)
        {
            LogAceita(logger, command.CotacaoId);
        }

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cotação {CotacaoId} aceita.")]
    private static partial void LogAceita(ILogger logger, Guid cotacaoId);
}

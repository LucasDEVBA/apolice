using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Sinistros.IniciarAnalise;

public sealed partial class IniciarAnaliseSinistroHandler(AcaoSinistro acao, ILogger<IniciarAnaliseSinistroHandler> logger)
    : ICommandHandler<IniciarAnaliseSinistroCommand, SinistroDto>
{
    public async Task<Result<SinistroDto>> HandleAsync(IniciarAnaliseSinistroCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resultado = await acao.ExecutarAsync(
            command.SinistroId,
            (sinistro, usuarioId, agora) => sinistro.IniciarAnalise(usuarioId, agora),
            AcoesAuditoria.SinistroEmAnalise,
            detalhes: null,
            cancellationToken);
        if (resultado.IsSuccess)
        {
            LogEmAnalise(logger, command.SinistroId);
        }

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sinistro {SinistroId} em análise.")]
    private static partial void LogEmAnalise(ILogger logger, Guid sinistroId);
}

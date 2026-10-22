using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Sinistros.Negar;

public sealed partial class NegarSinistroHandler(AcaoSinistro acao, ILogger<NegarSinistroHandler> logger)
    : ICommandHandler<NegarSinistroCommand, SinistroDto>
{
    public async Task<Result<SinistroDto>> HandleAsync(NegarSinistroCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // O motivo fica no histórico e no evento; a auditoria registra só a decisão (texto livre pode ter dado pessoal).
        var resultado = await acao.ExecutarAsync(
            command.SinistroId,
            (sinistro, usuarioId, agora) => sinistro.Negar(command.Motivo, usuarioId, agora),
            AcoesAuditoria.SinistroNegado,
            detalhes: null,
            cancellationToken);
        if (resultado.IsSuccess)
        {
            LogNegado(logger, command.SinistroId);
        }

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sinistro {SinistroId} negado.")]
    private static partial void LogNegado(ILogger logger, Guid sinistroId);
}

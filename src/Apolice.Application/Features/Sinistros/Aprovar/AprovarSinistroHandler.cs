using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Sinistros.Aprovar;

public sealed partial class AprovarSinistroHandler(AcaoSinistro acao, ILogger<AprovarSinistroHandler> logger)
    : ICommandHandler<AprovarSinistroCommand, SinistroDto>
{
    public async Task<Result<SinistroDto>> HandleAsync(AprovarSinistroCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resultado = await acao.ExecutarAsync(
            command.SinistroId,
            (sinistro, usuarioId, agora) => sinistro.Aprovar(command.ValorIndenizacao, command.Observacao, usuarioId, agora),
            AcoesAuditoria.SinistroAprovado,
            sinistro => DetalhesAuditoria.Json(new { valorIndenizacao = sinistro.ValorIndenizacao }),
            cancellationToken);
        if (resultado.IsSuccess)
        {
            LogAprovado(logger, command.SinistroId);
        }

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sinistro {SinistroId} aprovado.")]
    private static partial void LogAprovado(ILogger logger, Guid sinistroId);
}

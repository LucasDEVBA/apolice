using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Sinistros.RegistrarPagamento;

public sealed partial class RegistrarPagamentoSinistroHandler(AcaoSinistro acao, ILogger<RegistrarPagamentoSinistroHandler> logger)
    : ICommandHandler<RegistrarPagamentoSinistroCommand, SinistroDto>
{
    public async Task<Result<SinistroDto>> HandleAsync(RegistrarPagamentoSinistroCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var resultado = await acao.ExecutarAsync(
            command.SinistroId,
            (sinistro, usuarioId, agora) => sinistro.RegistrarPagamento(usuarioId, agora),
            AcoesAuditoria.SinistroPago,
            sinistro => DetalhesAuditoria.Json(new { valorPago = sinistro.ValorIndenizacao }),
            cancellationToken);
        if (resultado.IsSuccess)
        {
            LogPago(logger, command.SinistroId);
        }

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pagamento do sinistro {SinistroId} registrado.")]
    private static partial void LogPago(ILogger logger, Guid sinistroId);
}

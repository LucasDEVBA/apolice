using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Apolices.Cancelar;

public sealed partial class CancelarApoliceHandler(
    AcessoApolice acesso,
    IApoliceQueries consultas,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<CancelarApoliceHandler> logger) : ICommandHandler<CancelarApoliceCommand, ApoliceDto>
{
    public async Task<Result<ApoliceDto>> HandleAsync(CancelarApoliceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var carregada = await acesso.CarregarParaOperacaoAsync(command.ApoliceId, cancellationToken);
        if (carregada.IsFailure)
        {
            return carregada.Error;
        }

        var apolice = carregada.Value.Apolice;
        var agora = tempo.GetUtcNow();
        var cancelamento = apolice.Cancelar(command.Motivo, agora);
        if (cancelamento.IsFailure)
        {
            return cancelamento.Error;
        }

        var simulacao = cancelamento.Value;
        auditoria.Registrar(
            AcoesAuditoria.ApoliceCancelada,
            EntidadesAuditoria.Apolice,
            apolice.Id.ToString(),
            DetalhesAuditoria.Json(new { motivo = command.Motivo, metodo = simulacao.Metodo, valorRestituicao = simulacao.ValorRestituicao }));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogCancelada(logger, apolice.Id, command.Motivo, simulacao.Metodo);
        return Releitura.Obrigatoria(await consultas.ObterAsync(apolice.Id, null, Relogio.HojeEm(agora), cancellationToken), Entidades.Apolice);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Apólice {ApoliceId} cancelada ({Motivo}, método {Metodo}).")]
    private static partial void LogCancelada(ILogger logger, Guid apoliceId, MotivoCancelamento motivo, MetodoCancelamento metodo);
}

using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Tarifacao;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Apolices.Endossar;

public sealed partial class EndossarApoliceHandler(
    PreparacaoEndosso preparacao,
    ITarifador tarifador,
    IApoliceQueries consultas,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<EndossarApoliceHandler> logger) : ICommandHandler<EndossarApoliceCommand, ApoliceDto>
{
    public async Task<Result<ApoliceDto>> HandleAsync(EndossarApoliceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var preparado = await preparacao.PrepararAsync(command.ApoliceId, command.Tipo, command.CoberturaCodigo, command.Lmi, cancellationToken);
        if (preparado.IsFailure)
        {
            return preparado.Error;
        }

        var ((apolice, usuarioId), produto, solicitacao) = preparado.Value;
        var agora = tempo.GetUtcNow();
        var endosso = apolice.Endossar(solicitacao, produto, tarifador, usuarioId, agora);
        if (endosso.IsFailure)
        {
            return endosso.Error;
        }

        Auditar(apolice, endosso.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogEndossada(logger, apolice.Id, endosso.Value.Numero, endosso.Value.Tipo, endosso.Value.CoberturaCodigo);
        return Releitura.Obrigatoria(await consultas.ObterAsync(apolice.Id, null, Relogio.HojeEm(agora), cancellationToken), Entidades.Apolice);
    }

    private void Auditar(ApoliceSeguro apolice, Endosso endosso) =>
        auditoria.Registrar(
            AcoesAuditoria.ApoliceEndossada,
            EntidadesAuditoria.Apolice,
            apolice.Id.ToString(),
            DetalhesAuditoria.Json(new
            {
                endosso = endosso.Numero,
                tipo = endosso.Tipo,
                cobertura = endosso.CoberturaCodigo,
                lmiAnterior = endosso.LmiAnterior,
                lmiNovo = endosso.LmiNovo,
                ajusteTotal = endosso.AjusteTotal,
            }));

    [LoggerMessage(Level = LogLevel.Information, Message = "Apólice {ApoliceId} endossada: endosso {EndossoNumero} ({Tipo}) na cobertura {CoberturaCodigo}.")]
    private static partial void LogEndossada(ILogger logger, Guid apoliceId, int endossoNumero, TipoEndosso tipo, string coberturaCodigo);
}

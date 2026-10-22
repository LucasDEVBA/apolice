using System.Diagnostics;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Observability;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Sinistros;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Sinistros.Abrir;

public sealed partial class AbrirSinistroHandler(
    AcessoApolice acesso,
    ISinistroRepository sinistros,
    ISinistroQueries consultas,
    INumerador numerador,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<AbrirSinistroHandler> logger) : ICommandHandler<AbrirSinistroCommand, SinistroDto>
{
    public async Task<Result<SinistroDto>> HandleAsync(AbrirSinistroCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var carregada = await acesso.CarregarParaOperacaoAsync(command.ApoliceId, cancellationToken);
        if (carregada.IsFailure)
        {
            return carregada.Error;
        }

        using var atividade = ApoliceTelemetry.ActivitySource.StartActivity(ApoliceTelemetry.AtividadeAbrirSinistro);
        atividade?.SetTag(ApoliceTelemetry.TagProduto, carregada.Value.Apolice.ProdutoCodigo);
        var abertura = await AbrirAsync(carregada.Value, command, cancellationToken);
        if (abertura.IsFailure)
        {
            atividade?.SetStatus(ActivityStatusCode.Error, abertura.Error.Codigo);
            return abertura.Error;
        }

        var sinistro = abertura.Value;
        ApoliceTelemetry.SinistrosAbertos.Add(1, new KeyValuePair<string, object?>(ApoliceTelemetry.TagProduto, sinistro.ProdutoCodigo));
        LogAberto(logger, sinistro.Id, sinistro.Numero, sinistro.ApoliceId, sinistro.CoberturaCodigo);
        return Releitura.Obrigatoria(await consultas.ObterAsync(sinistro.Id, null, cancellationToken), Entidades.Sinistro);
    }

    private async Task<Result<Sinistro>> AbrirAsync(ApoliceOperavel carregada, AbrirSinistroCommand command, CancellationToken cancellationToken)
    {
        // O número sai antes das regras do domínio porque só o domínio as conhece e Sinistro.Abrir já exige o número:
        // avisos recusados deixam lacuna na sequência, aceita pelo SPEC 6.
        var agora = tempo.GetUtcNow();
        var ano = Relogio.HojeEm(agora).Year;
        var sequencial = await numerador.ProximoAsync(ChavesNumerador.Sinistro(ano), cancellationToken);
        var abertura = Sinistro.Abrir(
            carregada.Apolice,
            Sinistro.FormatarNumero(ano, sequencial),
            CodigoCatalogo.Normalizar(command.CoberturaCodigo) ?? string.Empty,
            command.DataOcorrencia,
            command.Descricao,
            command.ValorReclamado,
            carregada.UsuarioId,
            agora);
        if (abertura.IsFailure)
        {
            return abertura;
        }

        var sinistro = abertura.Value;
        sinistros.Adicionar(sinistro);
        auditoria.Registrar(
            AcoesAuditoria.SinistroAberto,
            EntidadesAuditoria.Sinistro,
            sinistro.Id.ToString(),
            DetalhesAuditoria.Json(new { numero = sinistro.Numero, apoliceId = sinistro.ApoliceId, cobertura = sinistro.CoberturaCodigo, valorReclamado = sinistro.ValorReclamado }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sinistro;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sinistro {SinistroId} ({Numero}) aberto na apólice {ApoliceId}, cobertura {CoberturaCodigo}.")]
    private static partial void LogAberto(ILogger logger, Guid sinistroId, string numero, Guid apoliceId, string coberturaCodigo);
}

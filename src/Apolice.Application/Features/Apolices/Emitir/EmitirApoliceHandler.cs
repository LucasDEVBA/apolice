using System.Diagnostics;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Observability;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Apolices.Emitir;

public sealed partial class EmitirApoliceHandler(
    ICotacaoRepository cotacoes,
    IApoliceRepository apolices,
    IApoliceQueries consultas,
    INumerador numerador,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider tempo,
    ILogger<EmitirApoliceHandler> logger) : ICommandHandler<EmitirApoliceCommand, ApoliceDto>
{
    public async Task<Result<ApoliceDto>> HandleAsync(EmitirApoliceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Autenticação antes de carregar: senão o anônimo receberia 404 para id inexistente e 401 para id existente.
        var autenticado = usuario.ExigirAutenticado();
        if (autenticado.IsFailure)
        {
            return autenticado.Error;
        }

        var cotacao = await cotacoes.ObterAsync(command.CotacaoId, cancellationToken);
        if (cotacao is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Cotacao);
        }

        var acesso = usuario.AutorizarCarteira(cotacao.CorretorId, Entidades.Cotacao);
        if (acesso.IsFailure)
        {
            return acesso.Error;
        }

        using var atividade = ApoliceTelemetry.ActivitySource.StartActivity(ApoliceTelemetry.AtividadeEmitirApolice);
        atividade?.SetTag(ApoliceTelemetry.TagProduto, cotacao.ProdutoCodigo);
        var emissao = await EmitirAsync(cotacao, command, cancellationToken);
        if (emissao.IsFailure)
        {
            atividade?.SetStatus(ActivityStatusCode.Error, emissao.Error.Codigo);
            return emissao.Error;
        }

        var apolice = emissao.Value;
        RegistrarMetricas(apolice);
        LogApoliceEmitida(logger, apolice.Id, apolice.Numero, apolice.ProdutoCodigo, acesso.Value);
        var hoje = Relogio.HojeEm(apolice.EmitidaEm);
        return Releitura.Obrigatoria(await consultas.ObterAsync(apolice.Id, null, hoje, cancellationToken), Entidades.Apolice);
    }

    private async Task<Result<ApoliceSeguro>> EmitirAsync(Cotacao cotacao, EmitirApoliceCommand command, CancellationToken cancellationToken)
    {
        var agora = tempo.GetUtcNow();

        // Checa a cotação antes de numerar: a recusa mais comum (cotação já emitida num duplo clique, recusada ou
        // expirada) não gasta número. As demais regras (início da vigência, parcelas) só o domínio conhece e rodam
        // dentro de Emitir, que já exige o número: essas recusas deixam lacuna, aceita pelo SPEC 6.
        var emitivel = cotacao.ValidarEmissao(agora);
        if (emitivel.IsFailure)
        {
            return emitivel.Error;
        }

        var ano = Relogio.HojeEm(agora).Year;
        var sequencial = await numerador.ProximoAsync(ChavesNumerador.Apolice(cotacao.ProdutoCodigo, ano), cancellationToken);
        var numero = ApoliceSeguro.FormatarNumero(cotacao.ProdutoCodigo, ano, sequencial);
        var emissao = ApoliceSeguro.Emitir(cotacao, numero, command.InicioVigencia, command.Parcelas, agora);
        if (emissao.IsFailure)
        {
            return emissao.Error;
        }

        var apolice = emissao.Value;
        apolices.Adicionar(apolice);
        auditoria.Registrar(
            AcoesAuditoria.ApoliceEmitida,
            EntidadesAuditoria.Apolice,
            apolice.Id.ToString(),
            DetalhesAuditoria.Json(new { numero, cotacaoId = cotacao.Id, premioTotal = apolice.PremioTotal, parcelas = apolice.Parcelas }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return apolice;
    }

    // Só depois do commit: métrica de negócio não pode contar uma emissão que não foi gravada.
    private static void RegistrarMetricas(ApoliceSeguro apolice)
    {
        var produto = new KeyValuePair<string, object?>(ApoliceTelemetry.TagProduto, apolice.ProdutoCodigo);
        ApoliceTelemetry.ApolicesEmitidas.Add(1, produto);
        ApoliceTelemetry.PremioEmitido.Add((double)apolice.PremioTotal, produto);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Apólice {ApoliceId} ({Numero}, {ProdutoCodigo}) emitida pelo usuário {UsuarioId}.")]
    private static partial void LogApoliceEmitida(ILogger logger, Guid apoliceId, string numero, string produtoCodigo, Guid usuarioId);
}

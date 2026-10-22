using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Cotacoes.Criar;

public sealed partial class CriarCotacaoHandler(
    TarifacaoCotacao tarifacao,
    ICotacaoRepository cotacoes,
    ICotacaoQueries consultas,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider tempo,
    ILogger<CriarCotacaoHandler> logger) : ICommandHandler<CriarCotacaoCommand, CotacaoDto>
{
    public async Task<Result<CotacaoDto>> HandleAsync(CriarCotacaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var corretor = usuario.ExigirPapel(Papel.Corretor, Papel.Admin);
        if (corretor.IsFailure)
        {
            return corretor.Error;
        }

        var agora = tempo.GetUtcNow();
        var tarifada = await tarifacao.TarifarParaGravarAsync(command.Cotacao, Relogio.HojeEm(agora), cancellationToken);
        if (tarifada.IsFailure)
        {
            return tarifada.Error;
        }

        var (produto, segurado, dadosRisco, resultado) = tarifada.Value;
        var cotacao = Cotacao.Criar(corretor.Value, produto, segurado, dadosRisco, resultado, agora);
        cotacoes.Adicionar(cotacao);
        auditoria.Registrar(
            AcoesAuditoria.CotacaoCriada,
            EntidadesAuditoria.Cotacao,
            cotacao.Id.ToString(),
            DetalhesAuditoria.Json(new { produto = cotacao.ProdutoCodigo, premioTotal = cotacao.PremioTotal }));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogCotacaoCriada(logger, cotacao.Id, cotacao.ProdutoCodigo, corretor.Value);
        return Releitura.Obrigatoria(await consultas.ObterAsync(cotacao.Id, null, agora, cancellationToken), Entidades.Cotacao);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cotação {CotacaoId} criada ({ProdutoCodigo}) pelo corretor {CorretorId}.")]
    private static partial void LogCotacaoCriada(ILogger logger, Guid cotacaoId, string produtoCodigo, Guid corretorId);
}

using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Produtos.AtualizarTaxa;

public sealed partial class AtualizarTaxaCoberturaHandler(
    IProdutoRepository produtos,
    ICatalogoProdutos catalogo,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider tempo,
    ILogger<AtualizarTaxaCoberturaHandler> logger) : ICommandHandler<AtualizarTaxaCoberturaCommand, ProdutoDto>
{
    public async Task<Result<ProdutoDto>> HandleAsync(AtualizarTaxaCoberturaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var admin = usuario.ExigirPapel(Papel.Admin);
        if (admin.IsFailure)
        {
            return admin.Error;
        }

        var codigo = CodigoCatalogo.Normalizar(command.ProdutoCodigo);
        var produto = codigo is null ? null : await produtos.ObterAsync(codigo, cancellationToken);
        if (produto is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Produto);
        }

        var resultado = await AtualizarAsync(produto, CodigoCatalogo.Normalizar(command.CoberturaCodigo) ?? string.Empty, command.Taxa, cancellationToken);
        return resultado.IsSuccess ? ProdutoMapeamento.ParaDto(produto) : resultado.Error;
    }

    private async Task<Result> AtualizarAsync(Produto produto, string coberturaCodigo, decimal novaTaxa, CancellationToken cancellationToken)
    {
        var eventosAntes = produto.DomainEvents.Count;
        var atualizacao = produto.AtualizarTaxa(coberturaCodigo, novaTaxa, tempo.GetUtcNow());

        // Quem diz se houve alteração é o domínio (o evento): mesma taxa não levanta evento, e então não há auditoria,
        // gravação nem invalidação de cache à toa. A regra "mesma taxa" não é repetida aqui.
        var alteracao = produto.DomainEvents.Skip(eventosAntes).OfType<TaxaCoberturaAtualizada>().SingleOrDefault();
        if (atualizacao.IsFailure || alteracao is null)
        {
            return atualizacao;
        }

        auditoria.Registrar(
            AcoesAuditoria.TaxaAtualizada,
            EntidadesAuditoria.Produto,
            produto.Codigo,
            DetalhesAuditoria.Json(new { cobertura = alteracao.CoberturaCodigo, taxaAnterior = alteracao.TaxaAnterior, taxaNova = alteracao.TaxaNova }));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await InvalidarCatalogoAsync(produto.Codigo);
        LogTaxaAtualizada(logger, produto.Codigo, alteracao.CoberturaCodigo, alteracao.TaxaNova);
        return Result.Success();
    }

    // Depois do commit (invalidar antes deixaria uma janela para o cache reler a taxa antiga) e sem derrubar a resposta:
    // a taxa já está gravada, um 500 levaria o cliente a repetir um pedido que vira no-op, e o cache expira sozinho.
    // CancellationToken.None: se o cliente desistir agora, a invalidação ainda precisa terminar.
    private async Task InvalidarCatalogoAsync(string produtoCodigo)
    {
        try
        {
            await catalogo.InvalidarAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInvalidacaoFalhou(logger, ex, produtoCodigo);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Taxa da cobertura {ProdutoCodigo}/{CoberturaCodigo} atualizada para {TaxaNova}.")]
    private static partial void LogTaxaAtualizada(ILogger logger, string produtoCodigo, string coberturaCodigo, decimal taxaNova);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Taxa do produto {ProdutoCodigo} gravada, mas a invalidação do catálogo falhou; o cache segue com a taxa antiga até expirar.")]
    private static partial void LogInvalidacaoFalhou(ILogger logger, Exception excecao, string produtoCodigo);
}

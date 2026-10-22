using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Features.Cotacoes;

public sealed record CotacaoTarifada(Produto Produto, Segurado Segurado, DadosRisco DadosRisco, ResultadoTarifacao Resultado)
{
    // O ToString do Email do domínio devolve o endereço inteiro: o gerado pelo record o levaria a um log (SPEC 9).
    public override string ToString() => $"CotacaoTarifada {{ Produto = {Produto.Codigo}, PremioTotal = {Resultado.PremioTotal} }}";
}

// Simular e criar cotação percorrem o mesmo caminho até o ResultadoTarifacao (validação, normalização, tarifação).
// Só muda de onde vem o produto: a simulação lê o catálogo cacheado; a criação, que grava o prêmio, lê o cadastro
// (fonte da verdade), porque o cache L1 de outra instância pode estar com a taxa antiga até expirar.
public sealed class TarifacaoCotacao(ICatalogoProdutos catalogo, IProdutoRepository produtos, ITarifador tarifador)
{
    public Task<Result<CotacaoTarifada>> SimularAsync(CotacaoRequest? request, DateOnly hoje, CancellationToken cancellationToken) =>
        TarifarAsync(request, hoje, catalogo.ObterAsync, cancellationToken);

    public Task<Result<CotacaoTarifada>> TarifarParaGravarAsync(CotacaoRequest? request, DateOnly hoje, CancellationToken cancellationToken) =>
        TarifarAsync(request, hoje, produtos.ObterAsync, cancellationToken);

    private async Task<Result<CotacaoTarifada>> TarifarAsync(
        CotacaoRequest? request,
        DateOnly hoje,
        Func<string, CancellationToken, Task<Produto?>> obterProduto,
        CancellationToken cancellationToken)
    {
        var codigo = ValidarPresenca(request);
        if (codigo.IsFailure)
        {
            return codigo.Error;
        }

        var produto = await obterProduto(codigo.Value, cancellationToken);
        if (produto is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Produto);
        }

        var dados = request!.Segurado;
        var segurado = Segurado.Criar(dados.Nome, dados.Cpf, dados.Email, dados.DataNascimento);
        if (segurado.IsFailure)
        {
            return segurado.Error;
        }

        var resultado = tarifador.Calcular(produto, segurado.Value, request.DadosRisco, Coberturas(request), hoje);
        return resultado.IsSuccess
            ? new CotacaoTarifada(produto, segurado.Value, request.DadosRisco, resultado.Value)
            : resultado.Error;
    }

    private static List<CoberturaSolicitada> Coberturas(CotacaoRequest request) =>
        [.. request.Coberturas.Select(c => new CoberturaSolicitada(CodigoCatalogo.Normalizar(c.Codigo) ?? string.Empty, c.Lmi))];

    // O domínio trata nulos como bug (ArgumentNullException); aqui eles ainda são entrada do cliente.
    // Devolve o código do produto já normalizado.
    private static Result<string> ValidarPresenca(CotacaoRequest? request)
    {
        var codigo = CodigoCatalogo.Normalizar(request?.ProdutoCodigo);
        if (codigo is null)
        {
            return ComumErros.DadosObrigatorios("o produto da cotação");
        }

        if (request!.Segurado is null)
        {
            return ComumErros.DadosObrigatorios("os dados do segurado");
        }

        if (request.DadosRisco is null)
        {
            return ComumErros.DadosObrigatorios("os dados de risco");
        }

        return request.Coberturas is null || request.Coberturas.Any(c => c is null)
            ? ComumErros.DadosObrigatorios("as coberturas")
            : codigo;
    }
}

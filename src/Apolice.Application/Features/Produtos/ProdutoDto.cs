using Apolice.Domain.Produtos;

namespace Apolice.Application.Features.Produtos;

public sealed record ProdutoDto(
    string Codigo,
    string Nome,
    string Descricao,
    decimal AliquotaIof,
    IReadOnlyList<CoberturaProdutoDto> Coberturas);

public sealed record CoberturaProdutoDto(
    string Codigo,
    string Nome,
    string Descricao,
    bool Obrigatoria,
    TipoCalculo TipoCalculo,
    decimal Taxa,
    decimal? PremioFixo,
    decimal LmiMinimo,
    decimal LmiMaximo,
    decimal? LmiFixo,
    bool PermiteSinistro);

public static class ProdutoMapeamento
{
    public static ProdutoDto ParaDto(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);
        return new ProdutoDto(produto.Codigo, produto.Nome, produto.Descricao, produto.AliquotaIof, [.. produto.Coberturas.Select(ParaDto)]);
    }

    public static CoberturaProdutoDto ParaDto(CoberturaProduto cobertura)
    {
        ArgumentNullException.ThrowIfNull(cobertura);
        return new CoberturaProdutoDto(
            cobertura.Codigo,
            cobertura.Nome,
            cobertura.Descricao,
            cobertura.Obrigatoria,
            cobertura.TipoCalculo,
            cobertura.Taxa,
            cobertura.PremioFixo,
            cobertura.LmiMinimo,
            cobertura.LmiMaximo,
            cobertura.LmiFixo,
            cobertura.PermiteSinistro);
    }
}

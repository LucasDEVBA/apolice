namespace Apolice.Application.Features.Produtos.AtualizarTaxa;

// Corpo de PUT /produtos/{codigo}/coberturas/{coberturaCodigo}/taxa; os códigos vêm da rota.
public sealed record AtualizarTaxaRequest(decimal Taxa);

public sealed record AtualizarTaxaCoberturaCommand(string ProdutoCodigo, string CoberturaCodigo, decimal Taxa);

namespace Apolice.Application.Features.Cotacoes.Simular;

// POST /cotacoes/simular: o corpo é o CotacaoRequest (validado por CotacaoRequestValidator); nada é gravado.
public sealed record SimularCotacaoCommand(CotacaoRequest Cotacao);

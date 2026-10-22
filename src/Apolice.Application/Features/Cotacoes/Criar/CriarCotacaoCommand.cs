namespace Apolice.Application.Features.Cotacoes.Criar;

// POST /cotacoes: o corpo é o CotacaoRequest (validado por CotacaoRequestValidator).
public sealed record CriarCotacaoCommand(CotacaoRequest Cotacao);

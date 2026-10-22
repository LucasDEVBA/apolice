namespace Apolice.Application.Features.Sinistros.Aprovar;

// Corpo de POST /sinistros/{id}/aprovacao; o id vem da rota.
public sealed record AprovarSinistroRequest(decimal ValorIndenizacao, string? Observacao);

public sealed record AprovarSinistroCommand(Guid SinistroId, decimal ValorIndenizacao, string? Observacao);

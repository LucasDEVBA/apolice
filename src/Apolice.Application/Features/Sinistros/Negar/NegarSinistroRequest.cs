namespace Apolice.Application.Features.Sinistros.Negar;

// Corpo de POST /sinistros/{id}/negativa; o id vem da rota.
public sealed record NegarSinistroRequest(string Motivo);

public sealed record NegarSinistroCommand(Guid SinistroId, string Motivo);

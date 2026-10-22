namespace Apolice.Application.Features.Sinistros.Abrir;

// Corpo de POST /sinistros (com Idempotency-Key opcional, tratado na API).
public sealed record AbrirSinistroRequest(
    Guid ApoliceId,
    string CoberturaCodigo,
    DateOnly DataOcorrencia,
    string Descricao,
    decimal ValorReclamado);

public sealed record AbrirSinistroCommand(
    Guid ApoliceId,
    string CoberturaCodigo,
    DateOnly DataOcorrencia,
    string Descricao,
    decimal ValorReclamado);

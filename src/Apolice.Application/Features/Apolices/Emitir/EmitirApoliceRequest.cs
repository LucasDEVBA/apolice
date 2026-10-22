namespace Apolice.Application.Features.Apolices.Emitir;

// Corpo de POST /apolices (com Idempotency-Key opcional, tratado na API).
public sealed record EmitirApoliceRequest(Guid CotacaoId, DateOnly InicioVigencia, int Parcelas);

public sealed record EmitirApoliceCommand(Guid CotacaoId, DateOnly InicioVigencia, int Parcelas);

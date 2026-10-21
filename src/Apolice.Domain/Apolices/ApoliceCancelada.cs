using Apolice.Domain.Comum;

namespace Apolice.Domain.Apolices;

public sealed record ApoliceCancelada(
    Guid ApoliceId,
    string Numero,
    MotivoCancelamento Motivo,
    decimal ValorRestituicao,
    string SeguradoNome,
    string SeguradoEmail,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);

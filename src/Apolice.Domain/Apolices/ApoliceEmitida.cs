using Apolice.Domain.Comum;

namespace Apolice.Domain.Apolices;

public sealed record ApoliceEmitida(
    Guid ApoliceId,
    string Numero,
    Guid CorretorId,
    string ProdutoCodigo,
    string SeguradoNome,
    string SeguradoEmail,
    decimal PremioTotal,
    DateOnly VigenciaInicio,
    DateOnly VigenciaFim,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);

using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.SimularEndosso;

public sealed record SimularEndossoCommand(Guid ApoliceId, TipoEndosso Tipo, string CoberturaCodigo, decimal? Lmi);

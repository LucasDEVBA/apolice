using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.Endossar;

public sealed record EndossarApoliceCommand(Guid ApoliceId, TipoEndosso Tipo, string CoberturaCodigo, decimal? Lmi);

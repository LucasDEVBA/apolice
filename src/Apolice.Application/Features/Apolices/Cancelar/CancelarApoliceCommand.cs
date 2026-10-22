using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.Cancelar;

public sealed record CancelarApoliceCommand(Guid ApoliceId, MotivoCancelamento Motivo);

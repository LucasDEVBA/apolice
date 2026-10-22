using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.SimularCancelamento;

public sealed record SimularCancelamentoCommand(Guid ApoliceId, MotivoCancelamento Motivo);

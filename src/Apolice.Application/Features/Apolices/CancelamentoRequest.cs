using Apolice.Domain.Apolices;
using FluentValidation;

namespace Apolice.Application.Features.Apolices;

// Corpo de POST /apolices/{id}/cancelamento/simular e /cancelamento; o id vem da rota.
public sealed record CancelamentoRequest(MotivoCancelamento Motivo);

public sealed class CancelamentoRequestValidator : AbstractValidator<CancelamentoRequest>
{
    public CancelamentoRequestValidator()
    {
        RuleFor(r => r.Motivo).IsInEnum().WithMessage("Motivo de cancelamento inválido.");
    }
}

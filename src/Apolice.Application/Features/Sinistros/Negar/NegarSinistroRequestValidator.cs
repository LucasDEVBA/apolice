using Apolice.Application.Common;
using FluentValidation;

namespace Apolice.Application.Features.Sinistros.Negar;

public sealed class NegarSinistroRequestValidator : AbstractValidator<NegarSinistroRequest>
{
    public NegarSinistroRequestValidator()
    {
        RuleFor(r => r.Motivo)
            .NotEmpty()
            .WithMessage("Informe o motivo da negativa.")
            .MaximumLength(LimitesEntrada.TextoLivre)
            .WithMessage($"O motivo da negativa deve ter no máximo {LimitesEntrada.TextoLivre} caracteres.");
    }
}

using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using FluentValidation;

namespace Apolice.Application.Features.Apolices;

public sealed class EndossoRequestValidator : AbstractValidator<EndossoRequest>
{
    public EndossoRequestValidator()
    {
        RuleFor(r => r.Tipo).IsInEnum().WithMessage("Tipo de endosso inválido.");
        RuleFor(r => r.CoberturaCodigo)
            .NotEmpty()
            .WithMessage("Informe o código da cobertura.")
            .MaximumLength(LimitesEntrada.CodigoCobertura)
            .WithMessage($"O código da cobertura deve ter no máximo {LimitesEntrada.CodigoCobertura} caracteres.");
        RuleFor(r => r.Lmi)
            .NotNull()
            .When(r => r.Tipo == TipoEndosso.AlteracaoLmi)
            .WithMessage("Informe o novo LMI.");
        RuleFor(r => r.Lmi)
            .GreaterThan(0)
            .When(r => r.Lmi is not null)
            .WithMessage("O LMI deve ser maior que zero.");
    }
}

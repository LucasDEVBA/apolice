using Apolice.Application.Common;
using FluentValidation;

namespace Apolice.Application.Features.Sinistros.Aprovar;

public sealed class AprovarSinistroRequestValidator : AbstractValidator<AprovarSinistroRequest>
{
    public AprovarSinistroRequestValidator()
    {
        RuleFor(r => r.ValorIndenizacao).GreaterThan(0).WithMessage("O valor da indenização deve ser maior que zero.");
        RuleFor(r => r.Observacao)
            .MaximumLength(LimitesEntrada.TextoLivre)
            .WithMessage($"A observação deve ter no máximo {LimitesEntrada.TextoLivre} caracteres.");
    }
}

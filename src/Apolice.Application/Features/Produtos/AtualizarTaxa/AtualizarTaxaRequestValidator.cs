using Apolice.Domain.Produtos;
using FluentValidation;

namespace Apolice.Application.Features.Produtos.AtualizarTaxa;

public sealed class AtualizarTaxaRequestValidator : AbstractValidator<AtualizarTaxaRequest>
{
    public AtualizarTaxaRequestValidator()
    {
        // A faixa válida é a do próprio domínio (CoberturaProduto.TaxaValida), não uma cópia aqui.
        RuleFor(r => r.Taxa)
            .Must(CoberturaProduto.TaxaValida)
            .WithMessage("A taxa deve ser maior que zero e no máximo 1 (fração).");
    }
}

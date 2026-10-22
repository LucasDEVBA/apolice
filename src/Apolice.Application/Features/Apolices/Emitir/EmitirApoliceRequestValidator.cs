using Apolice.Domain.Tarifacao;
using FluentValidation;

namespace Apolice.Application.Features.Apolices.Emitir;

// Janela do início de vigência e teto de parcelas dependem do relógio e da cotação: o domínio decide.
public sealed class EmitirApoliceRequestValidator : AbstractValidator<EmitirApoliceRequest>
{
    public EmitirApoliceRequestValidator()
    {
        RuleFor(r => r.CotacaoId).NotEmpty().WithMessage("Informe a cotação.");
        RuleFor(r => r.InicioVigencia).NotEmpty().WithMessage("Informe o início da vigência.");
        RuleFor(r => r.Parcelas)
            .GreaterThanOrEqualTo(ResultadoTarifacao.ParcelasMinimas)
            .WithMessage($"O número de parcelas deve ser ao menos {ResultadoTarifacao.ParcelasMinimas}.");
    }
}

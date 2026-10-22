using Apolice.Application.Common;
using FluentValidation;

namespace Apolice.Application.Features.Sinistros.Abrir;

// Forma e tamanho máximo (proteção do payload); vigência, cobertura e tamanho mínimo da descrição são do domínio.
public sealed class AbrirSinistroRequestValidator : AbstractValidator<AbrirSinistroRequest>
{
    public AbrirSinistroRequestValidator()
    {
        RuleFor(r => r.ApoliceId).NotEmpty().WithMessage("Informe a apólice.");
        RuleFor(r => r.CoberturaCodigo)
            .NotEmpty()
            .WithMessage("Informe o código da cobertura.")
            .MaximumLength(LimitesEntrada.CodigoCobertura)
            .WithMessage($"O código da cobertura deve ter no máximo {LimitesEntrada.CodigoCobertura} caracteres.");
        RuleFor(r => r.DataOcorrencia).NotEmpty().WithMessage("Informe a data da ocorrência.");
        RuleFor(r => r.Descricao)
            .NotEmpty()
            .WithMessage("Descreva a ocorrência.")
            .MaximumLength(LimitesEntrada.TextoLivre)
            .WithMessage($"A descrição deve ter no máximo {LimitesEntrada.TextoLivre} caracteres.");
        RuleFor(r => r.ValorReclamado).GreaterThan(0).WithMessage("O valor reclamado deve ser maior que zero.");
    }
}

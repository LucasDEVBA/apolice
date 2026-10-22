using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using FluentValidation;

namespace Apolice.Application.Features.Cotacoes;

// Valida forma e presença; faixas de valor, LMIs, coberturas obrigatórias e a coerência entre produto e dados de risco
// são regra da tarifação do domínio (Tarifacao.ProdutoIncompativel), que não é repetida aqui.
// Códigos passam pela mesma normalização da tarifação (CodigoCatalogo): "auto"/"casco" valem como "AUTO"/"CASCO".
public sealed class CotacaoRequestValidator : AbstractValidator<CotacaoRequest>
{
    public CotacaoRequestValidator()
    {
        RuleFor(r => r.ProdutoCodigo)
            .Must(codigo => CodigosProduto.EhValido(CodigoCatalogo.Normalizar(codigo)))
            .WithMessage("Produto inválido (use AUTO, RESID ou VIDA).");

        RuleFor(r => r.Segurado)
            .NotNull()
            .WithMessage("Informe os dados do segurado.")
            .SetValidator(new SeguradoRequestValidator());

        RuleFor(r => r.DadosRisco)
            .NotNull()
            .WithMessage("Informe os dados de risco.");

        RuleFor(r => r.Coberturas)
            .NotEmpty()
            .WithMessage("Informe ao menos uma cobertura.");

        RuleFor(r => r.Coberturas)
            .Must(SemCodigoRepetido)
            .When(r => r.Coberturas is not null)
            .WithMessage("Não informe a mesma cobertura mais de uma vez.");

        RuleForEach(r => r.Coberturas)
            .NotNull()
            .WithMessage("Cobertura inválida.")
            .ChildRules(cobertura =>
            {
                cobertura.RuleFor(c => c.Codigo)
                    .NotEmpty()
                    .WithMessage("Informe o código da cobertura.")
                    .MaximumLength(LimitesEntrada.CodigoCobertura)
                    .WithMessage($"O código da cobertura deve ter no máximo {LimitesEntrada.CodigoCobertura} caracteres.");
                cobertura.RuleFor(c => c.Lmi)
                    .GreaterThan(0)
                    .When(c => c.Lmi is not null)
                    .WithMessage("O LMI deve ser maior que zero.");
            });
    }

    // Itens nulos já têm a própria mensagem; aqui só conta repetição entre os preenchidos, já normalizados.
    private static bool SemCodigoRepetido(IReadOnlyList<CoberturaRequest> coberturas)
    {
        var codigos = coberturas.Where(c => c is not null).Select(c => CodigoCatalogo.Normalizar(c.Codigo)).ToList();
        return codigos.Distinct(StringComparer.Ordinal).Count() == codigos.Count;
    }
}

public sealed class SeguradoRequestValidator : AbstractValidator<SeguradoRequest>
{
    public SeguradoRequestValidator()
    {
        RuleFor(s => s.Nome)
            .NotEmpty()
            .WithMessage("Informe o nome do segurado.");

        // Reaproveita os VOs do domínio: dígitos verificadores e formato não são reimplementados aqui.
        RuleFor(s => s.Cpf)
            .Must(cpf => Cpf.Criar(cpf).IsSuccess)
            .WithMessage("CPF inválido.");

        RuleFor(s => s.Email)
            .Must(email => Email.Criar(email).IsSuccess)
            .WithMessage("E-mail inválido.");

        RuleFor(s => s.DataNascimento)
            .NotEmpty()
            .WithMessage("Informe a data de nascimento.");
    }
}

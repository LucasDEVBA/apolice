using Apolice.Application.Common;
using Apolice.Domain.Produtos;
using FluentValidation;

namespace Apolice.Application.Features.Cotacoes.Listar;

public sealed class ListarCotacoesQueryValidator : AbstractValidator<ListarCotacoesQuery>
{
    public ListarCotacoesQueryValidator()
    {
        RuleFor(q => q.Status).IsInEnum().WithMessage("Status inválido.");
        RuleFor(q => q.Produto)
            .Must(produto => CodigosProduto.EhValido(CodigoCatalogo.Normalizar(produto)))
            .When(q => !string.IsNullOrWhiteSpace(q.Produto))
            .WithMessage("Produto inválido (use AUTO, RESID ou VIDA).");
        RuleFor(q => q.Busca).MaximumLength(Paginacao.TamanhoMaximoBusca).WithMessage("Busca muito longa.");
        RuleFor(q => q.Cursor).Must(Cursor.EhValido).WithMessage("Cursor de paginação inválido.");
        RuleFor(q => q.Limite)
            .InclusiveBetween(Paginacao.LimiteMinimo, Paginacao.LimiteMaximo)
            .WithMessage($"O limite deve estar entre {Paginacao.LimiteMinimo} e {Paginacao.LimiteMaximo}.");
    }
}

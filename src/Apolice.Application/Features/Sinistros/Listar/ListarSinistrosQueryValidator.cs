using Apolice.Application.Common;
using FluentValidation;

namespace Apolice.Application.Features.Sinistros.Listar;

public sealed class ListarSinistrosQueryValidator : AbstractValidator<ListarSinistrosQuery>
{
    public ListarSinistrosQueryValidator()
    {
        RuleFor(q => q.Status).IsInEnum().WithMessage("Status inválido.");
        RuleFor(q => q.Busca).MaximumLength(Paginacao.TamanhoMaximoBusca).WithMessage("Busca muito longa.");
        RuleFor(q => q.Cursor).Must(Cursor.EhValido).WithMessage("Cursor de paginação inválido.");
        RuleFor(q => q.Limite)
            .InclusiveBetween(Paginacao.LimiteMinimo, Paginacao.LimiteMaximo)
            .WithMessage($"O limite deve estar entre {Paginacao.LimiteMinimo} e {Paginacao.LimiteMaximo}.");
    }
}

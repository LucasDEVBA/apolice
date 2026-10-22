using Apolice.Application.Common;
using FluentValidation;

namespace Apolice.Application.Features.Auditoria.Listar;

public sealed class ListarAuditoriaQueryValidator : AbstractValidator<ListarAuditoriaQuery>
{
    public ListarAuditoriaQueryValidator()
    {
        RuleFor(q => q.Cursor).Must(Cursor.EhValido).WithMessage("Cursor de paginação inválido.");
        RuleFor(q => q.Limite)
            .InclusiveBetween(Paginacao.LimiteMinimo, Paginacao.LimiteMaximo)
            .WithMessage($"O limite deve estar entre {Paginacao.LimiteMinimo} e {Paginacao.LimiteMaximo}.");
    }
}

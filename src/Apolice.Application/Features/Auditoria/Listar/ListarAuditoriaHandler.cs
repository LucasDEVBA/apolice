using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Features.Auditoria.Listar;

public sealed class ListarAuditoriaHandler(IAuditoriaQueries consultas, ICurrentUser usuario)
    : IQueryHandler<ListarAuditoriaQuery, Pagina<AuditoriaDto>>
{
    public async Task<Result<Pagina<AuditoriaDto>>> HandleAsync(ListarAuditoriaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var admin = usuario.ExigirPapel(Papel.Admin);
        if (admin.IsFailure)
        {
            return admin.Error;
        }

        var pagina = Paginacao.Resolver(query.Cursor, query.Limite);
        if (pagina.IsFailure)
        {
            return pagina.Error;
        }

        return await consultas.ListarAsync(pagina.Value.Apos, pagina.Value.Limite, cancellationToken);
    }
}

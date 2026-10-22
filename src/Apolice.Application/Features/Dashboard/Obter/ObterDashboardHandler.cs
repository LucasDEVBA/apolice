using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Dashboard.Obter;

public sealed class ObterDashboardHandler(IDashboardQueries consultas, ICurrentUser usuario, TimeProvider tempo)
    : IQueryHandler<ObterDashboardQuery, DashboardDto>
{
    public async Task<Result<DashboardDto>> HandleAsync(ObterDashboardQuery query, CancellationToken cancellationToken)
    {
        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        var agora = tempo.GetUtcNow();
        var metricas = await consultas.ObterAsync(new FiltroDashboard(escopo.Value.CorretorId, Relogio.HojeEm(agora), agora), cancellationToken);
        return DashboardDto.De(escopo.Value.EhGlobal ? EscopoDashboard.Global : EscopoDashboard.Carteira, metricas);
    }
}

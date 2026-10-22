using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Common;
using Apolice.Application.Features.Auditoria;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Queries;

internal sealed class AuditoriaQueries(NpgsqlDataSource fonte) : IAuditoriaQueries
{
    public async Task<Pagina<AuditoriaDto>> ListarAsync(PosicaoCursor? apos, int limite, CancellationToken cancellationToken)
    {
        var consulta = new ConsultaSql().Com("Limite", limite + 1).Apos("a.ocorrido_em", "a.id", apos);
        var sql = $"""
            SELECT a.id AS Id, u.nome AS UsuarioNome, a.acao AS Acao, a.entidade AS Entidade, a.entidade_id AS EntidadeId,
                   a.detalhes AS Detalhes, a.correlation_id AS CorrelationId, a.ocorrido_em AS OcorridoEm
            FROM auditoria a
            LEFT JOIN usuarios u ON u.id = a.usuario_id
            {consulta.Where}
            ORDER BY a.ocorrido_em DESC, a.id DESC
            LIMIT @Limite
            """;

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var linhas = await conexao.ListarAsync<LinhaAuditoria>(sql, consulta.Parametros, cancellationToken);
        IReadOnlyList<AuditoriaDto> itens =
        [
            .. linhas.Select(l => new AuditoriaDto(l.Id, l.UsuarioNome, l.Acao, l.Entidade, l.EntidadeId, l.Detalhes, l.CorrelationId, l.OcorridoEm)),
        ];
        return Pagina.Montar(itens, limite, r => new PosicaoCursor(r.OcorridoEm, r.Id));
    }
}

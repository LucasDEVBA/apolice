using System.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Common;
using Apolice.Application.Features.Sinistros;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Queries;

// O SinistroDto não traz o CorretorId: o filtro de carteira no SQL/LINQ é a única barreira de escopo aqui.
internal sealed class SinistroQueries(NpgsqlDataSource fonte, ApoliceDbContext db) : ISinistroQueries
{
    public async Task<Pagina<SinistroResumoDto>> ListarAsync(FiltroSinistros filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = new ConsultaSql().Com("Limite", filtro.Limite + 1);
        Filtrar(consulta, filtro);
        var sql = $"""
            SELECT {Leitura.ColunasResumoSinistro}
            FROM sinistros s
            {consulta.Where}
            ORDER BY s.aberto_em DESC, s.id DESC
            LIMIT @Limite
            """;

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var linhas = await conexao.ListarAsync<LinhaSinistroResumo>(sql, consulta.Parametros, cancellationToken);
        IReadOnlyList<SinistroResumoDto> itens = [.. linhas.Select(Leitura.ParaDto)];
        return Pagina.Montar(itens, filtro.Limite, r => new PosicaoCursor(r.AbertoEm, r.Id));
    }

    public async Task<SinistroDto?> ObterAsync(Guid id, Guid? corretorId, CancellationToken cancellationToken)
    {
        var consulta = db.Sinistros.AsNoTracking().Completos().Where(s => s.Id == id);
        if (corretorId is { } corretor)
        {
            consulta = consulta.Where(s => s.CorretorId == corretor);
        }

        var sinistro = await consulta.FirstOrDefaultAsync(cancellationToken);
        if (sinistro is null)
        {
            return null;
        }

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var nomes = await conexao.NomesAsync(produtoCodigo: null, sinistro.CorretorId, cancellationToken);
        var usuarios = await conexao.NomesUsuariosAsync(
            "SELECT h.usuario_id FROM sinistro_historico h WHERE h.sinistro_id = @Id",
            new { Id = sinistro.Id },
            cancellationToken);
        return SinistroMapeamento.ParaDto(sinistro, nomes.CorretorNome ?? Leitura.UsuarioDesconhecido, usuarios);
    }

    private static void Filtrar(ConsultaSql consulta, FiltroSinistros filtro)
    {
        if (filtro.CorretorId is { } corretor)
        {
            consulta.Onde("s.corretor_id = @CorretorId").Com("CorretorId", corretor, DbType.Guid);
        }

        if (filtro.Status is { } status)
        {
            consulta.Onde("s.status = @Status").Com("Status", status.ToString());
        }

        if (filtro.Busca is { } busca)
        {
            consulta.Onde(@"(s.numero ILIKE @Padrao ESCAPE '\' OR s.apolice_numero ILIKE @Padrao ESCAPE '\' OR s.segurado_nome ILIKE @Padrao ESCAPE '\')")
                .Com("Padrao", Busca.Padrao(busca));
        }

        consulta.Apos("s.aberto_em", "s.id", filtro.Apos);
    }
}

using System.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Domain.Apolices;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Queries;

internal sealed class ApoliceQueries(NpgsqlDataSource fonte, ApoliceDbContext db) : IApoliceQueries
{
    // Mesma regra de ApoliceSeguro.StatusEfetivo; a vigência é inclusiva nas duas pontas (Vigencia.Contem).
    private const string StatusEfetivo = """
        CASE WHEN a.status = 'Cancelada' THEN 'Cancelada'
             WHEN @Hoje < a.vigencia_inicio THEN 'Programada'
             WHEN @Hoje > a.vigencia_fim THEN 'Vencida'
             ELSE 'Ativa' END
        """;

    public async Task<Pagina<ApoliceResumoDto>> ListarAsync(FiltroApolices filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = new ConsultaSql()
            .Com("Hoje", filtro.Hoje, DbType.Date)
            .Com("Limite", filtro.Limite + 1);
        Filtrar(consulta, filtro);
        var sql = $"""
            SELECT a.id AS Id, a.numero AS Numero, a.produto_codigo AS ProdutoCodigo, a.segurado_nome AS SeguradoNome,
                   a.segurado_cpf AS SeguradoCpf, a.premio_total AS PremioTotal, a.vigencia_inicio AS VigenciaInicio,
                   a.vigencia_fim AS VigenciaFim, {StatusEfetivo} AS Status, u.nome AS CorretorNome, a.emitida_em AS EmitidaEm
            FROM apolices a
            JOIN usuarios u ON u.id = a.corretor_id
            {consulta.Where}
            ORDER BY a.emitida_em DESC, a.id DESC
            LIMIT @Limite
            """;

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var linhas = await conexao.ListarAsync<LinhaApoliceResumo>(sql, consulta.Parametros, cancellationToken);
        IReadOnlyList<ApoliceResumoDto> itens = [.. linhas.Select(ParaDto)];
        return Pagina.Montar(itens, filtro.Limite, r => new PosicaoCursor(r.EmitidaEm, r.Id));
    }

    // Troca consciente documentada na porta: o detalhe materializa o agregado (EF sem tracking) e usa
    // ApoliceMapeamento, para cronograma e status efetivo saírem das regras do domínio, não de uma cópia em SQL.
    public async Task<ApoliceDto?> ObterAsync(Guid id, Guid? corretorId, DateOnly hoje, CancellationToken cancellationToken)
    {
        var consulta = db.Apolices.AsNoTracking().Completas().Where(a => a.Id == id);
        if (corretorId is { } corretor)
        {
            consulta = consulta.Where(a => a.CorretorId == corretor);
        }

        var apolice = await consulta.FirstOrDefaultAsync(cancellationToken);
        if (apolice is null)
        {
            return null;
        }

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var complemento = await ComplementarAsync(conexao, apolice, cancellationToken);
        return ApoliceMapeamento.ParaDto(apolice, hoje, complemento);
    }

    private static async Task<ComplementoApolice> ComplementarAsync(NpgsqlConnection conexao, ApoliceSeguro apolice, CancellationToken cancellationToken)
    {
        var parametros = new { Id = apolice.Id };
        var nomes = await conexao.NomesAsync(apolice.ProdutoCodigo, apolice.CorretorId, cancellationToken);
        var autores = await conexao.NomesUsuariosAsync("SELECT e.criado_por_id FROM endossos e WHERE e.apolice_id = @Id", parametros, cancellationToken);
        var sinistros = await conexao.ListarAsync<LinhaSinistroResumo>(
            $"SELECT {Leitura.ColunasResumoSinistro} FROM sinistros s WHERE s.apolice_id = @Id ORDER BY s.aberto_em DESC, s.id DESC",
            parametros,
            cancellationToken);

        // Notificações da apólice e dos sinistros dela (referência = id do sinistro), em ordem de envio.
        var notificacoes = await conexao.ListarAsync<LinhaNotificacao>(
            """
            SELECT n.id AS Id, n.tipo AS Tipo, n.destinatario AS Destinatario, n.assunto AS Assunto, n.corpo AS Corpo, n.criada_em AS CriadaEm
            FROM notificacoes n
            WHERE n.referencia_id = @Id OR n.referencia_id IN (SELECT s.id FROM sinistros s WHERE s.apolice_id = @Id)
            ORDER BY n.criada_em, n.id
            """,
            parametros,
            cancellationToken);

        return new ComplementoApolice(
            nomes.ProdutoNome ?? apolice.ProdutoCodigo,
            nomes.CorretorNome ?? Leitura.UsuarioDesconhecido,
            autores,
            [.. sinistros.Select(Leitura.ParaDto)],
            [.. notificacoes.Select(Leitura.ParaDto)]);
    }

    private static void Filtrar(ConsultaSql consulta, FiltroApolices filtro)
    {
        if (filtro.CorretorId is { } corretor)
        {
            consulta.Onde("a.corretor_id = @CorretorId").Com("CorretorId", corretor, DbType.Guid);
        }

        if (filtro.Status is { } status)
        {
            consulta.Onde($"({StatusEfetivo}) = @Status").Com("Status", status.ToString());
        }

        if (filtro.ProdutoCodigo is { } produto)
        {
            consulta.Onde("a.produto_codigo = @ProdutoCodigo").Com("ProdutoCodigo", produto);
        }

        if (filtro.Busca is { } busca)
        {
            var cpf = Busca.PadraoCpf(busca);
            const string PorTexto = @"a.numero ILIKE @Padrao ESCAPE '\' OR a.segurado_nome ILIKE @Padrao ESCAPE '\'";
            consulta.Onde(cpf is null ? $"({PorTexto})" : $"({PorTexto} OR a.segurado_cpf LIKE @PadraoCpf)")
                .Com("Padrao", Busca.Padrao(busca));
            if (cpf is not null)
            {
                consulta.Com("PadraoCpf", cpf);
            }
        }

        consulta.Apos("a.emitida_em", "a.id", filtro.Apos);
    }

    private static ApoliceResumoDto ParaDto(LinhaApoliceResumo linha) =>
        new(
            linha.Id,
            linha.Numero,
            linha.ProdutoCodigo,
            linha.SeguradoNome,
            Mascaramento.Cpf(linha.SeguradoCpf),
            linha.PremioTotal,
            linha.VigenciaInicio,
            linha.VigenciaFim,
            Enum.Parse<StatusEfetivoApolice>(linha.Status),
            linha.CorretorNome,
            linha.EmitidaEm);
}

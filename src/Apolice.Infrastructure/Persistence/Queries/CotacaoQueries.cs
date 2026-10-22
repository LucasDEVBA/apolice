using System.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Common;
using Apolice.Application.Features.Cotacoes;
using Apolice.Domain.Cotacoes;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Queries;

internal sealed class CotacaoQueries(NpgsqlDataSource fonte, ApoliceDbContext db) : ICotacaoQueries
{
    // Mesma regra de Cotacao.StatusEfetivo: Expirada = Calculada/Aceita com Agora > ValidaAte.
    private const string StatusEfetivo = "CASE WHEN c.status IN ('Calculada', 'Aceita') AND c.valida_ate < @Agora THEN 'Expirada' ELSE c.status END";

    public async Task<Pagina<CotacaoResumoDto>> ListarAsync(FiltroCotacoes filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = new ConsultaSql()
            .Com("Agora", filtro.Agora.ToUniversalTime(), DbType.DateTimeOffset)
            .Com("Limite", filtro.Limite + 1);
        Filtrar(consulta, filtro);
        var sql = $"""
            SELECT c.id AS Id, c.produto_codigo AS ProdutoCodigo, c.segurado_nome AS SeguradoNome,
                   c.premio_total AS PremioTotal, {StatusEfetivo} AS Status, c.valida_ate AS ValidaAte,
                   c.criada_em AS CriadaEm, u.nome AS CorretorNome
            FROM cotacoes c
            JOIN usuarios u ON u.id = c.corretor_id
            {consulta.Where}
            ORDER BY c.criada_em DESC, c.id DESC
            LIMIT @Limite
            """;

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var linhas = await conexao.ListarAsync<LinhaCotacaoResumo>(sql, consulta.Parametros, cancellationToken);
        IReadOnlyList<CotacaoResumoDto> itens = [.. linhas.Select(ParaDto)];
        return Pagina.Montar(itens, filtro.Limite, r => new PosicaoCursor(r.CriadaEm, r.Id));
    }

    // Detalhe pelo agregado (EF sem tracking) e CotacaoMapeamento: o status efetivo sai da regra do domínio.
    public async Task<CotacaoDto?> ObterAsync(Guid id, Guid? corretorId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var consulta = db.Cotacoes.AsNoTracking().Completas().Where(c => c.Id == id);
        if (corretorId is { } corretor)
        {
            consulta = consulta.Where(c => c.CorretorId == corretor);
        }

        var cotacao = await consulta.FirstOrDefaultAsync(cancellationToken);
        if (cotacao is null)
        {
            return null;
        }

        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var nomes = await conexao.NomesAsync(cotacao.ProdutoCodigo, cotacao.CorretorId, cancellationToken);
        return CotacaoMapeamento.ParaDto(cotacao, nomes.ProdutoNome ?? cotacao.ProdutoCodigo, nomes.CorretorNome ?? Leitura.UsuarioDesconhecido, agora);
    }

    private static void Filtrar(ConsultaSql consulta, FiltroCotacoes filtro)
    {
        if (filtro.CorretorId is { } corretor)
        {
            consulta.Onde("c.corretor_id = @CorretorId").Com("CorretorId", corretor, DbType.Guid);
        }

        if (filtro.Status is { } status)
        {
            consulta.Onde($"{StatusEfetivo} = @Status").Com("Status", status.ToString());
        }

        if (filtro.ProdutoCodigo is { } produto)
        {
            consulta.Onde("c.produto_codigo = @ProdutoCodigo").Com("ProdutoCodigo", produto);
        }

        if (filtro.Busca is { } busca)
        {
            var cpf = Busca.PadraoCpf(busca);
            consulta.Onde(cpf is null
                    ? @"c.segurado_nome ILIKE @Padrao ESCAPE '\'"
                    : @"(c.segurado_nome ILIKE @Padrao ESCAPE '\' OR c.segurado_cpf LIKE @PadraoCpf)")
                .Com("Padrao", Busca.Padrao(busca));
            if (cpf is not null)
            {
                consulta.Com("PadraoCpf", cpf);
            }
        }

        consulta.Apos("c.criada_em", "c.id", filtro.Apos);
    }

    private static CotacaoResumoDto ParaDto(LinhaCotacaoResumo linha) =>
        new(
            linha.Id,
            linha.ProdutoCodigo,
            linha.SeguradoNome,
            linha.PremioTotal,
            Enum.Parse<StatusEfetivoCotacao>(linha.Status),
            linha.ValidaAte,
            linha.CriadaEm,
            linha.CorretorNome);
}

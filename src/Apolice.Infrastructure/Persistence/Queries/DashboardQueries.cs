using System.Data;
using System.Globalization;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Features.Dashboard;
using Apolice.Domain.Comum;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Queries;

// Fórmulas da porta (IDashboardQueries), todas no fuso de Brasília. Decisões onde a porta não fixa o detalhe:
// - "últimos 12 meses" = a mesma janela do gráfico (do 1º dia de 11 meses atrás até o fim do mês de Hoje), para o
//   prêmio por produto e a sinistralidade baterem com a soma das barras;
// - sinistralidade = indenizações de sinistros Aprovado/Pago ABERTOS na janela ÷ prêmio emitido na janela;
// - taxa de conversão = das cotações criadas nos últimos 90 dias, a fração que já virou apólice (status Emitida);
// - frações com 4 casas (0,2500 = 25%), arredondamento do SPEC (AwayFromZero).
internal sealed class DashboardQueries(NpgsqlDataSource fonte) : IDashboardQueries
{
    private const int MesesJanela = 12;
    private const int DiasConversao = 90;
    private const int CasasFracao = 4;
    private const string FormatoMes = "yyyy-MM";

    public async Task<MetricasDashboard> ObterAsync(FiltroDashboard filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var janela = Janela.De(filtro.Hoje);
        var parametros = Parametros(filtro, janela);
        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);

        var porProduto = await conexao.ListarAsync<LinhaProduto>(SqlPorProduto(filtro.CorretorId), parametros, cancellationToken);
        var mensal = await conexao.ListarAsync<LinhaMes>(SqlPremioMensal(filtro.CorretorId), parametros, cancellationToken);
        var cotacoes = (await conexao.ListarAsync<LinhaCotacoes>(SqlCotacoes(filtro.CorretorId), parametros, cancellationToken))[0];
        var sinistros = (await conexao.ListarAsync<LinhaSinistros>(SqlSinistros(filtro.CorretorId), parametros, cancellationToken))[0];

        var meses = janela.Meses().Select(m => new PremioMensalDto(m, mensal.Find(l => l.Mes == m)?.Valor ?? 0m)).ToList();
        var premioJanela = meses.Sum(m => m.Valor);
        return new MetricasDashboard(
            (int)porProduto.Sum(p => p.ApolicesAtivas),
            meses[^1].Valor,
            meses,
            (int)cotacoes.Abertas,
            Fracao(cotacoes.Emitidas90d, cotacoes.Criadas90d),
            (int)sinistros.EmAberto,
            Fracao(sinistros.Indenizacoes, premioJanela),
            [.. porProduto.Select(p => new ProdutoDashboardDto(p.ProdutoCodigo, p.ProdutoNome, (int)p.ApolicesAtivas, p.PremioEmitido))]);
    }

    private static Dapper.DynamicParameters Parametros(FiltroDashboard filtro, Janela janela)
    {
        var parametros = new Dapper.DynamicParameters();
        parametros.Add("Hoje", filtro.Hoje, DbType.Date);
        parametros.Add("Agora", filtro.Agora.ToUniversalTime(), DbType.DateTimeOffset);
        parametros.Add("InicioJanela", janela.Inicio, DbType.DateTimeOffset);
        parametros.Add("FimJanela", janela.Fim, DbType.DateTimeOffset);
        parametros.Add("InicioConversao", filtro.Agora.ToUniversalTime().AddDays(-DiasConversao), DbType.DateTimeOffset);
        if (filtro.CorretorId is { } corretor)
        {
            parametros.Add("CorretorId", corretor, DbType.Guid);
        }

        return parametros;
    }

    // LEFT JOIN a partir de produtos: todo produto aparece, mesmo sem apólice na carteira.
    private static string SqlPorProduto(Guid? corretorId) => $"""
        SELECT p.codigo AS ProdutoCodigo, p.nome AS ProdutoNome,
               COALESCE(SUM(CASE WHEN a.status <> 'Cancelada' AND a.vigencia_inicio <= @Hoje AND a.vigencia_fim >= @Hoje THEN 1 ELSE 0 END), 0) AS ApolicesAtivas,
               COALESCE(SUM(CASE WHEN a.emitida_em >= @InicioJanela AND a.emitida_em < @FimJanela THEN a.premio_total ELSE 0 END), 0) AS PremioEmitido
        FROM produtos p
        LEFT JOIN apolices a ON a.produto_codigo = p.codigo{(corretorId is null ? string.Empty : " AND a.corretor_id = @CorretorId")}
        GROUP BY p.codigo, p.nome
        ORDER BY p.codigo
        """;

    // Mês civil em Brasília com offset fixo (sem tzdata no banco nem no Lambda), como Relogio.HojeEm.
    private static string SqlPremioMensal(Guid? corretorId) => $"""
        SELECT to_char((a.emitida_em AT TIME ZONE 'UTC') + INTERVAL '{(int)Relogio.OffsetBrasilia.TotalHours} hours', 'YYYY-MM') AS Mes,
               SUM(a.premio_total) AS Valor
        FROM apolices a
        WHERE a.emitida_em >= @InicioJanela AND a.emitida_em < @FimJanela{FiltroCorretor("a", corretorId)}
        GROUP BY 1
        """;

    private static string SqlCotacoes(Guid? corretorId) => $"""
        SELECT COALESCE(SUM(CASE WHEN c.status IN ('Calculada', 'Aceita') AND c.valida_ate >= @Agora THEN 1 ELSE 0 END), 0) AS Abertas,
               COALESCE(SUM(CASE WHEN c.criada_em >= @InicioConversao THEN 1 ELSE 0 END), 0) AS Criadas90d,
               COALESCE(SUM(CASE WHEN c.criada_em >= @InicioConversao AND c.status = 'Emitida' THEN 1 ELSE 0 END), 0) AS Emitidas90d
        FROM cotacoes c
        WHERE (c.criada_em >= @InicioConversao OR c.status IN ('Calculada', 'Aceita')){FiltroCorretor("c", corretorId)}
        """;

    private static string SqlSinistros(Guid? corretorId) => $"""
        SELECT COALESCE(SUM(CASE WHEN s.status IN ('Aberto', 'EmAnalise') THEN 1 ELSE 0 END), 0) AS EmAberto,
               COALESCE(SUM(CASE WHEN s.status IN ('Aprovado', 'Pago') AND s.aberto_em >= @InicioJanela AND s.aberto_em < @FimJanela
                                 THEN s.valor_indenizacao ELSE 0 END), 0) AS Indenizacoes
        FROM sinistros s
        WHERE TRUE{FiltroCorretor("s", corretorId)}
        """;

    private static string FiltroCorretor(string alias, Guid? corretorId) =>
        corretorId is null ? string.Empty : $" AND {alias}.corretor_id = @CorretorId";

    private static decimal Fracao(decimal parte, decimal todo) =>
        todo == 0 ? 0m : Math.Round(parte / todo, CasasFracao, MidpointRounding.AwayFromZero);

    private sealed record Janela(DateTimeOffset Inicio, DateTimeOffset Fim, DateOnly PrimeiroMes)
    {
        public static Janela De(DateOnly hoje)
        {
            var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
            var primeiro = mesAtual.AddMonths(1 - MesesJanela);
            return new Janela(InicioDoDia(primeiro), InicioDoDia(mesAtual.AddMonths(1)), primeiro);
        }

        public IEnumerable<string> Meses() =>
            Enumerable.Range(0, MesesJanela).Select(i => PrimeiroMes.AddMonths(i).ToString(FormatoMes, CultureInfo.InvariantCulture));

        private static DateTimeOffset InicioDoDia(DateOnly dia) =>
            new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), Relogio.OffsetBrasilia).ToUniversalTime();
    }

    private sealed class LinhaProduto
    {
        public string ProdutoCodigo { get; init; } = string.Empty;

        public string ProdutoNome { get; init; } = string.Empty;

        public long ApolicesAtivas { get; init; }

        public decimal PremioEmitido { get; init; }
    }

    private sealed class LinhaMes
    {
        public string Mes { get; init; } = string.Empty;

        public decimal Valor { get; init; }
    }

    private sealed class LinhaCotacoes
    {
        public long Abertas { get; init; }

        public long Criadas90d { get; init; }

        public long Emitidas90d { get; init; }
    }

    private sealed class LinhaSinistros
    {
        public long EmAberto { get; init; }

        public decimal Indenizacoes { get; init; }
    }
}

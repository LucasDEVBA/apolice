using System.Text.RegularExpressions;

namespace Apolice.Infrastructure.Persistence.Migracao;

// Adapta, comando a comando, o DDL que o provedor Postgres gera para o que o Aurora DSQL aceita:
// índices viram ASYNC (o DSQL não cria índice síncrono em tabela existente) e tudo ganha IF NOT EXISTS, para a
// migração ser reexecutável depois de uma falha no meio (cada comando é uma transação própria no DSQL).
// Recursos que o DSQL não tem (sequence/identity, trigger, extension, jsonb, índice parcial) são recusados ANTES de
// tocar o banco: melhor falhar no deploy com mensagem clara do que deixar o schema pela metade.
internal static partial class TransformadorSqlDsql
{
    private const int TimeoutRegexMs = 200;

    public static string Transformar(string comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var sql = comando.Trim();
        ValidarCompatibilidade(sql);
        sql = CriarTabela().Replace(sql, "CREATE TABLE IF NOT EXISTS ", 1);
        sql = CriarIndice().Replace(sql, m => $"CREATE {m.Groups["unico"].Value}INDEX ASYNC IF NOT EXISTS ", 1);
        sql = AdicionarColuna().Replace(sql, "ADD COLUMN IF NOT EXISTS ");
        return sql;
    }

    public static bool CriaIndice(string sql) => IndiceAsync().IsMatch(sql);

    private static void ValidarCompatibilidade(string sql)
    {
        var incompativel = Incompativel().Match(sql);
        if (incompativel.Success)
        {
            throw new InvalidOperationException($"DDL incompatível com o Aurora DSQL ('{incompativel.Value}'): {Resumo(sql)}");
        }

        if (CriarIndice().IsMatch(sql) && IndiceParcial().IsMatch(sql))
        {
            throw new InvalidOperationException($"Índice parcial não é suportado pelo Aurora DSQL: {Resumo(sql)}");
        }
    }

    private static string Resumo(string sql) => sql.Length > 200 ? sql[..200] + "…" : sql;

    [GeneratedRegex(@"^CREATE TABLE (?!IF NOT EXISTS )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex CriarTabela();

    [GeneratedRegex(@"^CREATE (?<unico>UNIQUE )?INDEX (?!ASYNC )(?:IF NOT EXISTS )?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex CriarIndice();

    [GeneratedRegex(@"^CREATE (UNIQUE )?INDEX ASYNC ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex IndiceAsync();

    [GeneratedRegex(@"\bADD COLUMN (?!IF NOT EXISTS )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex AdicionarColuna();

    [GeneratedRegex(@"\b(CREATE SEQUENCE|GENERATED (ALWAYS|BY DEFAULT) AS IDENTITY|SERIAL|BIGSERIAL|SMALLSERIAL|CREATE (OR REPLACE )?TRIGGER|CREATE EXTENSION|JSONB)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex Incompativel();

    [GeneratedRegex(@"\)\s*WHERE\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex IndiceParcial();
}

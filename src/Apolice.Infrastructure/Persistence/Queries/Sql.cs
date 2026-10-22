using System.Data;
using Apolice.Application.Common;
using Dapper;

namespace Apolice.Infrastructure.Persistence.Queries;

// Montagem do WHERE só com fragmentos fixos deste código e valores sempre por parâmetro: nenhum texto do usuário
// entra no SQL. Montar dinamicamente (em vez de "@x IS NULL OR ...") dá ao planner um plano por forma de filtro.
internal sealed class ConsultaSql
{
    private readonly List<string> _condicoes = [];

    public DynamicParameters Parametros { get; } = new();

    public string Where => _condicoes.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", _condicoes);

    public ConsultaSql Onde(string condicao)
    {
        _condicoes.Add(condicao);
        return this;
    }

    public ConsultaSql Com(string nome, object? valor, DbType? tipo = null)
    {
        Parametros.Add(nome, valor, tipo);
        return this;
    }

    // Keyset por (instante, id) DESC com OR explícito: o DSQL não otimiza comparação de row-value como o PostgreSQL.
    public ConsultaSql Apos(string colunaInstante, string colunaId, PosicaoCursor? apos)
    {
        if (apos is null)
        {
            return this;
        }

        return Onde($"({colunaInstante} < @AposInstante OR ({colunaInstante} = @AposInstante AND {colunaId} < @AposId))")
            .Com("AposInstante", apos.Instante.ToUniversalTime(), DbType.DateTimeOffset)
            .Com("AposId", apos.Id, DbType.Guid);
    }
}

internal static class Busca
{
    private const string Coringa = "%";

    // ILIKE com os curingas do usuário escapados: "50%" procura o texto "50%", não "50" seguido de qualquer coisa.
    public static string Padrao(string busca) => Coringa + Escapar(busca) + Coringa;

    // CPF é gravado só com dígitos: "123.456" também deve achar. Busca com letras nunca casa com CPF.
    public static string? PadraoCpf(string busca)
    {
        if (!busca.All(c => char.IsAsciiDigit(c) || c is '.' or '-' or ' '))
        {
            return null;
        }

        var digitos = new string([.. busca.Where(char.IsAsciiDigit)]);
        return digitos.Length == 0 ? null : Coringa + digitos + Coringa;
    }

    private static string Escapar(string busca) =>
        busca.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}

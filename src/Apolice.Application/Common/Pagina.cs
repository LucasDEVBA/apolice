namespace Apolice.Application.Common;

public sealed record Pagina<T>(IReadOnlyList<T> Itens, string? ProximoCursor);

public static class Pagina
{
    public static Pagina<T> Vazia<T>() => new([], null);

    // Contrato com a leitura (keyset por (criado_em, id) DESC): a consulta busca "limite + 1" linhas;
    // a linha extra só prova que há próxima página e não é devolvida.
    public static Pagina<T> Montar<T>(IReadOnlyList<T> linhas, int limite, Func<T, PosicaoCursor> chave)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        ArgumentNullException.ThrowIfNull(chave);
        ArgumentOutOfRangeException.ThrowIfLessThan(limite, Paginacao.LimiteMinimo);

        if (linhas.Count <= limite)
        {
            return new Pagina<T>(linhas, null);
        }

        IReadOnlyList<T> itens = [.. linhas.Take(limite)];
        var ultima = chave(itens[^1]);
        return new Pagina<T>(itens, Cursor.Codificar(ultima.Instante, ultima.Id));
    }
}

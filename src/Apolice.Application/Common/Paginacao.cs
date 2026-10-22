using Apolice.Domain.Comum;

namespace Apolice.Application.Common;

public sealed record ParametrosPagina(PosicaoCursor? Apos, int Limite);

public static class Paginacao
{
    public const int LimiteMinimo = 1;
    public const int LimitePadrao = 20;
    public const int LimiteMaximo = 100;
    public const int TamanhoMaximoBusca = 100;

    // Também roda no handler (e não só no validator): o handler não pode confiar que a borda validou.
    public static Result<ParametrosPagina> Resolver(string? cursor, int? limite)
    {
        var tamanho = limite ?? LimitePadrao;
        if (tamanho is < LimiteMinimo or > LimiteMaximo)
        {
            return ComumErros.LimiteInvalido(LimiteMinimo, LimiteMaximo);
        }

        // "?cursor=" vazio na query string equivale a começar do início.
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return new ParametrosPagina(null, tamanho);
        }

        var posicao = Cursor.Decodificar(cursor);
        return posicao.IsSuccess ? new ParametrosPagina(posicao.Value, tamanho) : posicao.Error;
    }

    public static string? NormalizarFiltro(string? busca) => string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();
}

using Apolice.Domain.Comum;

namespace Apolice.Domain.Cotacoes;

public static class CotacaoErros
{
    public static Error Expirada { get; } =
        Error.RegraDeNegocio("Cotacao.Expirada", "A cotação expirou; calcule uma nova cotação.");

    public static Error StatusInvalido(StatusCotacao status) =>
        Error.RegraDeNegocio("Cotacao.StatusInvalido", $"A operação não é permitida para cotação com status {status}.");
}

namespace Apolice.Domain.Comum;

public sealed record Error(string Codigo, string Mensagem, ErrorType Tipo)
{
    public static Error Validacao(string codigo, string mensagem) => new(codigo, mensagem, ErrorType.Validacao);

    public static Error NaoEncontrado(string codigo, string mensagem) => new(codigo, mensagem, ErrorType.NaoEncontrado);

    public static Error Conflito(string codigo, string mensagem) => new(codigo, mensagem, ErrorType.Conflito);

    public static Error Proibido(string codigo, string mensagem) => new(codigo, mensagem, ErrorType.Proibido);

    public static Error RegraDeNegocio(string codigo, string mensagem) => new(codigo, mensagem, ErrorType.RegraDeNegocio);

    public static Error NaoAutorizado(string codigo, string mensagem) => new(codigo, mensagem, ErrorType.NaoAutorizado);
}

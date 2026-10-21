using Apolice.Domain.Comum;

namespace Apolice.Domain.Usuarios;

public static class UsuarioErros
{
    public static Error SenhaHashObrigatorio { get; } =
        Error.Validacao("Usuario.SenhaHashObrigatorio", "O hash da senha é obrigatório.");

    public static Error PapelInvalido { get; } =
        Error.Validacao("Usuario.PapelInvalido", "Papel de usuário inválido.");

    public static Error NomeInvalido(int minimo, int maximo) =>
        Error.Validacao("Usuario.NomeInvalido", $"O nome deve ter entre {minimo} e {maximo} caracteres.");
}

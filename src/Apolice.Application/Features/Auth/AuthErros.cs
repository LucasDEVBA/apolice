using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Auth;

// "Não autenticado" é transversal e fica em ComumErros.NaoAutenticado.
public static class AuthErros
{
    // E-mail inexistente, senha errada e usuário inativo: a mesma resposta, para não permitir enumerar contas.
    public static Error CredenciaisInvalidas { get; } =
        Error.NaoAutorizado("Auth.CredenciaisInvalidas", "E-mail ou senha inválidos.");

    // 401 com código próprio (SPEC 5), e não 423/429: o cliente trata como falha de login com mensagem específica.
    public static Error UsuarioBloqueado { get; } =
        Error.NaoAutorizado("Auth.UsuarioBloqueado", "Acesso temporariamente bloqueado por excesso de tentativas. Tente novamente em alguns minutos.");

    public static Error RefreshInvalido { get; } =
        Error.NaoAutorizado("Auth.RefreshInvalido", "Sua sessão expirou. Entre novamente.");

    public static Error RefreshReutilizado { get; } =
        Error.NaoAutorizado("Auth.RefreshReutilizado", "Sua sessão foi encerrada por segurança. Entre novamente.");

    public static Error PersonaInvalida { get; } =
        Error.Validacao("Auth.PersonaInvalida", "Persona de demonstração inválida (use corretora, corretor, analista ou admin).");
}

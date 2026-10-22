using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Auth.Login;

// Tempo de resposta: e-mail inexistente e conta inativa gastam o mesmo PBKDF2 de uma senha errada (SimularVerificacao),
// mas a gravação da falha/auditoria é I/O a mais nos caminhos de conta existente. Diferença aceita: o próprio bloqueio
// exigido pelo SPEC 5 (UsuarioBloqueado na 5ª falha) já revela que a conta existe, com só cinco tentativas.
public sealed partial class LoginHandler(
    IUsuarioRepository usuarios,
    IPasswordHasher hasher,
    EmissorSessao emissor,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<LoginHandler> logger) : ICommandHandler<LoginCommand, ResultadoAutenticacao>
{
    // Tentativas em paralelo leem a mesma versão do usuário e só uma grava; sem refazer, as demais falhas não contariam
    // e o bloqueio seria contornável em rajadas. Cada conflito prova que outra alteração foi gravada: com o teto acima do
    // limiar de bloqueio do domínio, esgotá-lo exige a conta já bloqueada — e aí nada mais altera o usuário nem conflita.
    private const int TentativasMaximas = 10;

    public async Task<Result<ResultadoAutenticacao>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var senha = command.Senha ?? string.Empty;
        var usuario = await BuscarAsync(command.Email, cancellationToken);
        if (usuario is null)
        {
            hasher.SimularVerificacao(senha);
            LogEmailDesconhecido(logger, Mascaramento.Email(command.Email));
            return AuthErros.CredenciaisInvalidas;
        }

        var agora = tempo.GetUtcNow();
        var senhaConfere = VerificarSenha(usuario, senha, agora);
        return await ComRetentativaAsync(usuario, atual => AplicarAsync(atual, senhaConfere, agora, cancellationToken), cancellationToken);
    }

    private async Task<Usuario?> BuscarAsync(string? email, CancellationToken cancellationToken)
    {
        var valido = Email.Criar(email);
        return valido.IsSuccess ? await usuarios.ObterPorEmailAsync(valido.Value, cancellationToken) : null;
    }

    // A senha é verificada uma única vez (o PBKDF2 é o custo dominante); as retentativas só refazem a gravação.
    // Conta bloqueada nem verifica: a resposta já é UsuarioBloqueado, qualquer que seja a senha.
    private bool VerificarSenha(Usuario usuario, string senha, DateTimeOffset agora)
    {
        if (!usuario.Ativo)
        {
            hasher.SimularVerificacao(senha);
            return false;
        }

        return !usuario.EstaBloqueado(agora) && hasher.Verificar(usuario.SenhaHash, senha);
    }

    private async Task<Result<ResultadoAutenticacao>> ComRetentativaAsync(
        Usuario usuario,
        Func<Usuario, Task<Result<ResultadoAutenticacao>>> operacao,
        CancellationToken cancellationToken)
    {
        var atual = usuario;
        for (var tentativa = 1; tentativa < TentativasMaximas; tentativa++)
        {
            try
            {
                return await operacao(atual);
            }
            catch (ConflitoDeConcorrenciaException)
            {
                LogConflito(logger, usuario.Id, tentativa);
                unitOfWork.DescartarAlteracoes();
                var recarregado = await usuarios.ObterAsync(usuario.Id, cancellationToken);
                if (recarregado is null)
                {
                    return AuthErros.CredenciaisInvalidas;
                }

                atual = recarregado;
            }
        }

        return await operacao(atual);
    }

    // Reavaliado a cada tentativa sobre o estado recarregado: outra requisição pode ter bloqueado ou desativado a conta.
    // Inativo vem antes do bloqueio para a conta desativada nunca responder diferente de uma inexistente.
    private Task<Result<ResultadoAutenticacao>> AplicarAsync(Usuario usuario, bool senhaConfere, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (!usuario.Ativo)
        {
            LogInativo(logger, usuario.Id);
            return RecusarAsync(usuario, AcoesAuditoria.LoginInativo, AuthErros.CredenciaisInvalidas, cancellationToken);
        }

        if (usuario.EstaBloqueado(agora))
        {
            LogBloqueado(logger, usuario.Id);
            return RecusarAsync(usuario, AcoesAuditoria.LoginBloqueado, AuthErros.UsuarioBloqueado, cancellationToken);
        }

        return senhaConfere ? ConcluirAsync(usuario, agora, cancellationToken) : RegistrarFalhaAsync(usuario, agora, cancellationToken);
    }

    // Sem contar falha: em conta inativa contaria para um bloqueio que revelaria a conta; durante o bloqueio, o domínio
    // já ignora (senão o atacante prolongaria o bloqueio da vítima). Só a auditoria registra a tentativa.
    private async Task<Result<ResultadoAutenticacao>> RecusarAsync(Usuario usuario, string acao, Error erro, CancellationToken cancellationToken)
    {
        auditoria.Registrar(acao, EntidadesAuditoria.Usuario, usuario.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return erro;
    }

    private async Task<Result<ResultadoAutenticacao>> RegistrarFalhaAsync(Usuario usuario, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        usuario.RegistrarFalhaLogin(agora);
        var bloqueado = usuario.EstaBloqueado(agora);
        auditoria.Registrar(bloqueado ? AcoesAuditoria.UsuarioBloqueado : AcoesAuditoria.LoginFalhou, EntidadesAuditoria.Usuario, usuario.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogSenhaIncorreta(logger, usuario.Id, bloqueado);
        return bloqueado ? AuthErros.UsuarioBloqueado : AuthErros.CredenciaisInvalidas;
    }

    private async Task<Result<ResultadoAutenticacao>> ConcluirAsync(Usuario usuario, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        usuario.RegistrarLoginSucesso();
        var sessao = emissor.Emitir(usuario, familiaId: null, agora);
        auditoria.Registrar(AcoesAuditoria.Login, EntidadesAuditoria.Usuario, usuario.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogSucesso(logger, usuario.Id, usuario.Papel);
        return sessao.Resultado;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login recusado: e-mail {EmailMascarado} não cadastrado.")]
    private static partial void LogEmailDesconhecido(ILogger logger, string emailMascarado);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login recusado: usuário {UsuarioId} bloqueado.")]
    private static partial void LogBloqueado(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login recusado: usuário {UsuarioId} inativo.")]
    private static partial void LogInativo(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login recusado: senha incorreta para o usuário {UsuarioId} (bloqueado: {Bloqueado}).")]
    private static partial void LogSenhaIncorreta(ILogger logger, Guid usuarioId, bool bloqueado);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login do usuário {UsuarioId} ({Papel}).")]
    private static partial void LogSucesso(ILogger logger, Guid usuarioId, Papel papel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login do usuário {UsuarioId}: conflito de concorrência na tentativa {Tentativa}; refazendo sobre o estado gravado.")]
    private static partial void LogConflito(ILogger logger, Guid usuarioId, int tentativa);
}

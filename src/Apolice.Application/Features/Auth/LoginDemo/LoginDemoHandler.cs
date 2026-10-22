using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Auth.LoginDemo;

// O bloqueio por senha errada fica fora deste fluxo de propósito: ele protege a senha, e a demo não usa senha. Os
// e-mails das personas são públicos; respeitar o bloqueio deixaria qualquer um tirar a demo do ar errando a senha
// delas, e zerar o contador aqui (RegistrarLoginSucesso) ajudaria quem tenta adivinhá-la.
public sealed partial class LoginDemoHandler(
    IFeatureFlags flags,
    IUsuarioRepository usuarios,
    EmissorSessao emissor,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<LoginDemoHandler> logger) : ICommandHandler<LoginDemoCommand, ResultadoAutenticacao>
{
    public async Task<Result<ResultadoAutenticacao>> HandleAsync(LoginDemoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var habilitado = await flags.ExigirAsync(FeatureFlags.LoginDemo, cancellationToken);
        if (habilitado.IsFailure)
        {
            return habilitado.Error;
        }

        var usuario = await BuscarAsync(command.Persona, cancellationToken);
        if (usuario.IsFailure)
        {
            return usuario.Error;
        }

        if (!usuario.Value.Ativo)
        {
            return AuthErros.CredenciaisInvalidas;
        }

        var sessao = emissor.Emitir(usuario.Value, familiaId: null, tempo.GetUtcNow());
        auditoria.Registrar(AcoesAuditoria.LoginDemo, EntidadesAuditoria.Usuario, usuario.Value.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogLoginDemo(logger, usuario.Value.Id, usuario.Value.Papel);
        return sessao.Resultado;
    }

    private async Task<Result<Usuario>> BuscarAsync(string? persona, CancellationToken cancellationToken)
    {
        var email = PersonasDemo.EmailDe(persona);
        if (email is null)
        {
            return AuthErros.PersonaInvalida;
        }

        // Sem o seed, a persona não existe: 404 explica melhor do que "credenciais inválidas" numa demo.
        var usuario = await usuarios.ObterPorEmailAsync(Email.Criar(email).Value, cancellationToken);
        return usuario is null ? ComumErros.NaoEncontrado(Entidades.Usuario) : usuario;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login de demonstração do usuário {UsuarioId} ({Papel}).")]
    private static partial void LogLoginDemo(ILogger logger, Guid usuarioId, Papel papel);
}

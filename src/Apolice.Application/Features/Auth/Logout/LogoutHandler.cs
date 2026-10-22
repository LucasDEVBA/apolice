using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Auth.Logout;

public sealed partial class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    RevogacaoSessao revogacao,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<LogoutHandler> logger) : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Sem token ou token desconhecido: nada a revogar, e o cliente limpa o cookie do mesmo jeito.
        var token = string.IsNullOrWhiteSpace(command.RefreshToken)
            ? null
            : await refreshTokens.ObterPorHashAsync(RefreshToken.CalcularHash(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return Unit.Value;
        }

        await revogacao.RevogarFamiliaAsync(token, tempo.GetUtcNow(), cancellationToken);
        auditoria.Registrar(AcoesAuditoria.Logout, EntidadesAuditoria.Usuario, token.UsuarioId.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogLogout(logger, token.UsuarioId);
        return Unit.Value;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Logout do usuário {UsuarioId}: sessão revogada.")]
    private static partial void LogLogout(ILogger logger, Guid usuarioId);
}

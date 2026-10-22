using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Auth.Refresh;

public sealed partial class RefreshHandler(
    IRefreshTokenRepository refreshTokens,
    IUsuarioRepository usuarios,
    EmissorSessao emissor,
    RevogacaoSessao revogacao,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<RefreshHandler> logger) : ICommandHandler<RefreshCommand, ResultadoAutenticacao>
{
    public async Task<Result<ResultadoAutenticacao>> HandleAsync(RefreshCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return AuthErros.RefreshInvalido;
        }

        var atual = await refreshTokens.ObterPorHashAsync(RefreshToken.CalcularHash(command.RefreshToken), cancellationToken);
        if (atual is null)
        {
            return AuthErros.RefreshInvalido;
        }

        var agora = tempo.GetUtcNow();

        // Refinamento do "token já revogado → reuso" do SPEC 5: só o token ROTACIONADO (com substituto) prova reuso.
        // Revogado sem substituto = família já encerrada (logout, reuso anterior, usuário inativo): o cookie velho de
        // outra aba voltando não é indício de roubo e não deve gerar alarme nem auditoria de reuso.
        if (atual.SubstituidoPorId is not null)
        {
            return await TratarReusoAsync(atual, agora, cancellationToken);
        }

        return atual.EstaAtivo(agora) ? await RotacionarAsync(atual, agora, cancellationToken) : AuthErros.RefreshInvalido;
    }

    // Token já usado de volta = indício de roubo: derruba todas as sessões da família.
    private async Task<Result<ResultadoAutenticacao>> TratarReusoAsync(RefreshToken token, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await revogacao.RevogarFamiliaAsync(token, agora, cancellationToken);
        auditoria.Registrar(
            AcoesAuditoria.RefreshReutilizado,
            EntidadesAuditoria.Usuario,
            token.UsuarioId.ToString(),
            DetalhesAuditoria.Json(new { familiaId = token.FamiliaId }));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogReuso(logger, token.UsuarioId, token.FamiliaId);
        return AuthErros.RefreshReutilizado;
    }

    private async Task<Result<ResultadoAutenticacao>> RotacionarAsync(RefreshToken atual, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObterAsync(atual.UsuarioId, cancellationToken);

        // Bloqueio por senha errada NÃO derruba a sessão: senão um atacante deslogaria a vítima só errando a senha dela.
        if (usuario is not { Ativo: true })
        {
            return await EncerrarSessaoDeUsuarioIndisponivelAsync(atual, agora, cancellationToken);
        }

        var sessao = emissor.Emitir(usuario, atual.FamiliaId, agora);
        var rotacao = atual.Rotacionar(sessao.Token.Id, agora);
        if (rotacao.IsFailure)
        {
            throw new InvalidOperationException($"Rotação recusada para um token conferido como ativo: {rotacao.Error.Codigo}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sessao.Resultado;
    }

    // Revogar a família de quem foi desativado é evento de sessão sensível: o administrador precisa enxergá-lo na auditoria.
    private async Task<Result<ResultadoAutenticacao>> EncerrarSessaoDeUsuarioIndisponivelAsync(RefreshToken atual, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await revogacao.RevogarFamiliaAsync(atual, agora, cancellationToken);
        auditoria.Registrar(
            AcoesAuditoria.SessaoRevogadaUsuarioInativo,
            EntidadesAuditoria.Usuario,
            atual.UsuarioId.ToString(),
            DetalhesAuditoria.Json(new { familiaId = atual.FamiliaId }));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogUsuarioIndisponivel(logger, atual.UsuarioId);
        return AuthErros.RefreshInvalido;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reuso de refresh token do usuário {UsuarioId}: família {FamiliaId} revogada.")]
    private static partial void LogReuso(ILogger logger, Guid usuarioId, Guid familiaId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh recusado: usuário {UsuarioId} inexistente ou inativo; sessão revogada.")]
    private static partial void LogUsuarioIndisponivel(ILogger logger, Guid usuarioId);
}

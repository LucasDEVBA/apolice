using System.Security.Cryptography;
using System.Text;
using Apolice.Domain.Comum;

namespace Apolice.Domain.Usuarios;

public sealed class RefreshToken : AggregateRoot<Guid>
{
    private const int TamanhoHashHex = 64;

    public static readonly TimeSpan ValidadePadrao = TimeSpan.FromDays(7);

    private RefreshToken()
    {
        TokenHash = null!;
    }

    private RefreshToken(Guid usuarioId, string tokenHash, Guid familiaId, DateTimeOffset agora, TimeSpan validade)
        : base(Guid.CreateVersion7(agora))
    {
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        FamiliaId = familiaId;
        CriadoEm = agora;
        ExpiraEm = agora.Add(validade);
    }

    public Guid UsuarioId { get; private set; }

    // Só o hash é guardado: um vazamento do banco não entrega tokens utilizáveis.
    public string TokenHash { get; private set; }

    // Família = cadeia de rotações de um mesmo login; reuso de um token antigo revoga a família toda.
    public Guid FamiliaId { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    public Guid? SubstituidoPorId { get; private set; }

    public bool EstaRevogado => RevogadoEm is not null;

    // Dados inválidos aqui vêm do próprio servidor (o token é gerado na infraestrutura): bug, não fluxo previsível.
    public static RefreshToken Criar(Guid usuarioId, string tokenHash, Guid familiaId, DateTimeOffset agora, TimeSpan validade)
    {
        if (usuarioId == Guid.Empty || familiaId == Guid.Empty)
        {
            throw new ArgumentException("Usuário e família do refresh token são obrigatórios.", nameof(usuarioId));
        }

        if (tokenHash is null || tokenHash.Length != TamanhoHashHex || !tokenHash.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("O hash deve ser SHA-256 em hexadecimal minúsculo.", nameof(tokenHash));
        }

        if (validade <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(validade), "A validade deve ser positiva.");
        }

        return new RefreshToken(usuarioId, tokenHash, familiaId, agora, validade);
    }

    public static RefreshToken NovaFamilia(Guid usuarioId, string tokenHash, DateTimeOffset agora, TimeSpan validade) =>
        Criar(usuarioId, tokenHash, Guid.CreateVersion7(agora), agora, validade);

    public static string CalcularHash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public bool EstaAtivo(DateTimeOffset agora) => RevogadoEm is null && agora < ExpiraEm;

    // Idempotente: revogar a família pode passar de novo por tokens já revogados sem apagar a data original.
    public void Revogar(DateTimeOffset agora) => RevogadoEm ??= agora;

    public Result Rotacionar(Guid novoId, DateTimeOffset agora)
    {
        if (novoId == Guid.Empty || novoId == Id)
        {
            throw new ArgumentException("Id do token substituto inválido.", nameof(novoId));
        }

        if (EstaRevogado)
        {
            return RefreshTokenErros.Revogado;
        }

        if (agora >= ExpiraEm)
        {
            return RefreshTokenErros.Expirado;
        }

        RevogadoEm = agora;
        SubstituidoPorId = novoId;
        return Result.Success();
    }
}

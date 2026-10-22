using System.Security.Cryptography;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace Apolice.Infrastructure.Security;

// PBKDF2 do ASP.NET Core Identity (formato V3, parâmetros embutidos no próprio hash).
internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private const int TamanhoSenhaFicticia = 32;
    private readonly PasswordHasher<Usuario> _hasher = new();

    // Calculado uma vez por instância, com os mesmos parâmetros dos hashes reais: verificar contra ele custa o mesmo
    // que uma verificação de verdade, sem que exista senha que o satisfaça.
    private readonly Lazy<string> _hashFicticio;

    public PasswordHasherAdapter()
    {
        _hashFicticio = new Lazy<string>(() => _hasher.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoSenhaFicticia))));
    }

    // O PasswordHasher padrão não usa o usuário (só subclasses poderiam): null é seguro aqui.
    public string Hash(string senha)
    {
        ArgumentNullException.ThrowIfNull(senha);
        return _hasher.HashPassword(null!, senha);
    }

    public bool Verificar(string senhaHash, string senha)
    {
        ArgumentNullException.ThrowIfNull(senha);
        if (string.IsNullOrEmpty(senhaHash))
        {
            return false;
        }

        try
        {
            return _hasher.VerifyHashedPassword(null!, senhaHash, senha) is not PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Hash gravado fora do formato (não é base64): credencial inválida, nunca exceção para o login.
            return false;
        }
    }

    public void SimularVerificacao(string senha) =>
        _hasher.VerifyHashedPassword(null!, _hashFicticio.Value, senha ?? string.Empty);
}

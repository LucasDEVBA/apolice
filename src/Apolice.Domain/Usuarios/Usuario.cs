using Apolice.Domain.Comum;

namespace Apolice.Domain.Usuarios;

public sealed class Usuario : AggregateRoot<Guid>
{
    private const int NomeTamanhoMinimo = 3;
    private const int NomeTamanhoMaximo = 120;
    private const int FalhasParaBloqueio = 5;
    private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

    private Usuario()
    {
        Nome = null!;
        Email = null!;
        SenhaHash = null!;
    }

    private Usuario(string nome, Email email, string senhaHash, Papel papel, DateTimeOffset agora)
        : base(Guid.CreateVersion7(agora))
    {
        Nome = nome;
        Email = email;
        SenhaHash = senhaHash;
        Papel = papel;
        Ativo = true;
    }

    public string Nome { get; private set; }

    public Email Email { get; private set; }

    public string SenhaHash { get; private set; }

    public Papel Papel { get; private set; }

    public bool Ativo { get; private set; }

    public int FalhasLogin { get; private set; }

    public DateTimeOffset? BloqueadoAte { get; private set; }

    public static Result<Usuario> Criar(string nome, Email email, string senhaHash, Papel papel, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(email);

        var nomeNormalizado = nome?.Trim();
        if (nomeNormalizado is null || nomeNormalizado.Length is < NomeTamanhoMinimo or > NomeTamanhoMaximo)
        {
            return UsuarioErros.NomeInvalido(NomeTamanhoMinimo, NomeTamanhoMaximo);
        }

        if (string.IsNullOrWhiteSpace(senhaHash))
        {
            return UsuarioErros.SenhaHashObrigatorio;
        }

        return Enum.IsDefined(papel)
            ? new Usuario(nomeNormalizado, email, senhaHash, papel, agora)
            : UsuarioErros.PapelInvalido;
    }

    public bool EstaBloqueado(DateTimeOffset agora) => BloqueadoAte is { } bloqueadoAte && agora < bloqueadoAte;

    // Falha durante o bloqueio é ignorada: senão o atacante prolongaria o bloqueio da vítima indefinidamente.
    public void RegistrarFalhaLogin(DateTimeOffset agora)
    {
        if (EstaBloqueado(agora))
        {
            return;
        }

        FalhasLogin++;
        if (FalhasLogin < FalhasParaBloqueio)
        {
            return;
        }

        BloqueadoAte = agora.Add(DuracaoBloqueio);
        FalhasLogin = 0;
    }

    public void RegistrarLoginSucesso()
    {
        FalhasLogin = 0;
        BloqueadoAte = null;
    }
}

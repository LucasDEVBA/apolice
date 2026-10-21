using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Time.Testing;

namespace Apolice.Domain.Tests.Usuarios;

public sealed class UsuarioTests
{
    private static readonly Email EmailAna = Email.Criar("ana.corretora@apolice.dev").Value;
    private static readonly TimeSpan Bloqueio = TimeSpan.FromMinutes(15);

    [Fact]
    public void Deve_nascer_ativo_e_sem_falhas_quando_criado()
    {
        var usuario = Usuario.Criar("  Ana Ribeiro ", EmailAna, "hash-pbkdf2", Papel.Corretor, Cenarios.Agora).DeveTerSucesso();

        usuario.Id.Version.ShouldBe(7);
        (usuario.Nome, usuario.Email, usuario.SenhaHash, usuario.Papel).ShouldBe(("Ana Ribeiro", EmailAna, "hash-pbkdf2", Papel.Corretor));
        usuario.Ativo.ShouldBeTrue();
        usuario.FalhasLogin.ShouldBe(0);
        usuario.BloqueadoAte.ShouldBeNull();
        usuario.EstaBloqueado(Cenarios.Agora).ShouldBeFalse();
    }

    [Fact]
    public void Deve_rejeitar_quando_nome_senha_ou_papel_invalidos()
    {
        Usuario.Criar("An", EmailAna, "hash", Papel.Corretor, Cenarios.Agora).DeveFalharCom("Usuario.NomeInvalido", ErrorType.Validacao);
        Usuario.Criar(null!, EmailAna, "hash", Papel.Corretor, Cenarios.Agora).DeveFalharCom("Usuario.NomeInvalido");
        Usuario.Criar("Ana Ribeiro", EmailAna, " ", Papel.Corretor, Cenarios.Agora).DeveFalharCom("Usuario.SenhaHashObrigatorio");
        Usuario.Criar("Ana Ribeiro", EmailAna, "hash", (Papel)99, Cenarios.Agora).DeveFalharCom("Usuario.PapelInvalido");
        Should.Throw<ArgumentNullException>(() => Usuario.Criar("Ana Ribeiro", null!, "hash", Papel.Corretor, Cenarios.Agora));
    }

    [Fact]
    public void Deve_so_contar_quando_ate_a_quarta_falha_consecutiva()
    {
        var (usuario, tempo) = NovoUsuario();

        RegistrarFalhas(usuario, tempo, 4);

        usuario.FalhasLogin.ShouldBe(4);
        usuario.BloqueadoAte.ShouldBeNull();
        usuario.EstaBloqueado(tempo.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void Deve_bloquear_15_minutos_e_zerar_contador_quando_quinta_falha()
    {
        var (usuario, tempo) = NovoUsuario();
        RegistrarFalhas(usuario, tempo, 4);
        var quintaFalha = tempo.GetUtcNow();

        usuario.RegistrarFalhaLogin(quintaFalha);

        usuario.BloqueadoAte.ShouldBe(quintaFalha + Bloqueio);
        usuario.FalhasLogin.ShouldBe(0);
        usuario.EstaBloqueado(quintaFalha).ShouldBeTrue();
        usuario.EstaBloqueado(quintaFalha + Bloqueio - TimeSpan.FromTicks(1)).ShouldBeTrue();
        usuario.EstaBloqueado(quintaFalha + Bloqueio).ShouldBeFalse(); // libera exatamente aos 15 min
    }

    [Fact]
    public void Deve_ignorar_falhas_quando_ja_bloqueado()
    {
        var (usuario, tempo) = NovoUsuario();
        RegistrarFalhas(usuario, tempo, 5);
        var bloqueadoAte = usuario.BloqueadoAte;

        tempo.Advance(TimeSpan.FromMinutes(10));
        usuario.RegistrarFalhaLogin(tempo.GetUtcNow());

        // Senão um atacante prolongaria o bloqueio da vítima indefinidamente.
        usuario.BloqueadoAte.ShouldBe(bloqueadoAte);
        usuario.FalhasLogin.ShouldBe(0);
    }

    [Fact]
    public void Deve_recomecar_a_contagem_quando_bloqueio_expira()
    {
        var (usuario, tempo) = NovoUsuario();
        RegistrarFalhas(usuario, tempo, 5);

        tempo.Advance(Bloqueio);
        usuario.RegistrarFalhaLogin(tempo.GetUtcNow());

        usuario.FalhasLogin.ShouldBe(1);
        usuario.EstaBloqueado(tempo.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void Deve_zerar_falhas_e_bloqueio_quando_login_com_sucesso()
    {
        var (usuario, tempo) = NovoUsuario();
        RegistrarFalhas(usuario, tempo, 5);

        usuario.RegistrarLoginSucesso();

        usuario.FalhasLogin.ShouldBe(0);
        usuario.BloqueadoAte.ShouldBeNull();
    }

    [Fact]
    public void Deve_exigir_cinco_falhas_seguidas_quando_houve_sucesso_no_meio()
    {
        var (usuario, tempo) = NovoUsuario();
        RegistrarFalhas(usuario, tempo, 4);
        usuario.RegistrarLoginSucesso();

        RegistrarFalhas(usuario, tempo, 4);

        usuario.BloqueadoAte.ShouldBeNull();
        usuario.FalhasLogin.ShouldBe(4);
    }

    private static (Usuario Usuario, FakeTimeProvider Tempo) NovoUsuario()
    {
        var tempo = new FakeTimeProvider(Cenarios.Agora);
        return (Usuario.Criar("Ana Ribeiro", EmailAna, "hash", Papel.Corretor, tempo.GetUtcNow()).Value, tempo);
    }

    private static void RegistrarFalhas(Usuario usuario, FakeTimeProvider tempo, int quantidade)
    {
        for (var i = 0; i < quantidade; i++)
        {
            tempo.Advance(TimeSpan.FromSeconds(10));
            usuario.RegistrarFalhaLogin(tempo.GetUtcNow());
        }
    }
}

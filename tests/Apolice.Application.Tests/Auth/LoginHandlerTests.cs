using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Auth;

public sealed class LoginHandlerTests : TesteBase
{
    private const int TentativasAntesDoBloqueio = 4;
    private const string EmailAna = "ana.corretora@apolice.dev";

    private LoginHandler Handler => Amb.Servico<LoginHandler>();

    [Fact]
    public async Task Deve_emitir_sessao_quando_credenciais_corretas()
    {
        var resultado = await Handler.HandleAsync(new LoginCommand("  ANA.Corretora@apolice.dev ", Ambiente.SenhaPadrao), Ct);

        var sessao = resultado.DeveTerSucesso();
        sessao.Resposta.Usuario.ShouldBe(new UsuarioDto(Amb.Ana.Id, "Ana Ribeiro", EmailAna, Papel.Corretor));
        sessao.Resposta.ExpiraEm.ShouldBe(Amb.Agora.AddMinutes(15));
        sessao.RefreshTokenExpiraEm.ShouldBe(Amb.Agora.AddDays(7));
        var token = Amb.Banco.RefreshTokens.ShouldHaveSingleItem();
        token.TokenHash.ShouldBe(RefreshToken.CalcularHash(sessao.RefreshToken));
        token.UsuarioId.ShouldBe(Amb.Ana.Id);
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.Login]);
        sessao.ToString().ShouldNotContain(sessao.RefreshToken);
    }

    [Fact]
    public async Task Deve_retornar_o_mesmo_erro_quando_email_inexistente_ou_senha_errada()
    {
        var inexistente = await Handler.HandleAsync(new LoginCommand("ninguem@apolice.dev", Ambiente.SenhaPadrao), Ct);
        var senhaErrada = await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);
        var emailInvalido = await Handler.HandleAsync(new LoginCommand("sem-arroba", Ambiente.SenhaPadrao), Ct);

        inexistente.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo, ErrorType.NaoAutorizado);
        senhaErrada.Error.ShouldBe(inexistente.Error);
        emailInvalido.Error.ShouldBe(inexistente.Error);
        Amb.Banco.RefreshTokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deve_gastar_uma_verificacao_de_senha_quando_email_inexistente_ou_senha_errada()
    {
        await Handler.HandleAsync(new LoginCommand("ninguem@apolice.dev", "x"), Ct);

        Amb.Hasher.VerificacoesSimuladas.ShouldBe(1);
        Amb.Hasher.Verificacoes.ShouldBe(0);

        await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);

        Amb.Hasher.VerificacoesSimuladas.ShouldBe(1);
        Amb.Hasher.Verificacoes.ShouldBe(1);
    }

    [Fact]
    public async Task Deve_registrar_falha_e_auditar_quando_senha_errada()
    {
        var savesAntes = Amb.Banco.TotalSaves;

        await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);

        Amb.Ana.FalhasLogin.ShouldBe(1);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
        Amb.Auditoria.Registros.ShouldHaveSingleItem().ShouldBe(new RegistroAuditoria(AcoesAuditoria.LoginFalhou, EntidadesAuditoria.Usuario, Amb.Ana.Id.ToString(), null));
    }

    [Fact]
    public async Task Deve_bloquear_quando_quinta_senha_errada()
    {
        await ErrarSenhaAsync(TentativasAntesDoBloqueio);

        var quinta = await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);

        quinta.DeveFalharCom(AuthErros.UsuarioBloqueado.Codigo, ErrorType.NaoAutorizado);
        Amb.Ana.EstaBloqueado(Amb.Agora).ShouldBeTrue();
        Amb.Auditoria.Acoes.Last().ShouldBe(AcoesAuditoria.UsuarioBloqueado);
    }

    [Fact]
    public async Task Deve_recusar_senha_correta_quando_usuario_bloqueado_e_liberar_apos_15_minutos()
    {
        await ErrarSenhaAsync(TentativasAntesDoBloqueio + 1);

        var bloqueado = await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct);
        Amb.Avancar(TimeSpan.FromMinutes(15));
        var liberado = await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct);

        bloqueado.DeveFalharCom(AuthErros.UsuarioBloqueado.Codigo);
        liberado.DeveTerSucesso();
        Amb.Ana.FalhasLogin.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_auditar_sem_contar_falha_nem_verificar_senha_quando_conta_ja_bloqueada()
    {
        Reflexao.Definir(Amb.Ana, nameof(Usuario.BloqueadoAte), Amb.Agora.AddMinutes(10));
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, "qualquer"), Ct);

        resultado.DeveFalharCom(AuthErros.UsuarioBloqueado.Codigo, ErrorType.NaoAutorizado);
        Amb.Auditoria.Registros.ShouldHaveSingleItem().ShouldBe(new RegistroAuditoria(AcoesAuditoria.LoginBloqueado, EntidadesAuditoria.Usuario, Amb.Ana.Id.ToString(), null));
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
        Amb.Ana.FalhasLogin.ShouldBe(0);
        Amb.Ana.BloqueadoAte.ShouldBe(Amb.Agora.AddMinutes(10));
        Amb.Hasher.Verificacoes.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_zerar_o_contador_quando_login_correto_entre_falhas()
    {
        await ErrarSenhaAsync(2);
        (await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct)).DeveTerSucesso();

        await ErrarSenhaAsync(TentativasAntesDoBloqueio);

        Amb.Ana.EstaBloqueado(Amb.Agora).ShouldBeFalse();
        Amb.Ana.FalhasLogin.ShouldBe(TentativasAntesDoBloqueio);
    }

    [Theory]
    [InlineData(Ambiente.SenhaPadrao)]
    [InlineData("errada")]
    public async Task Deve_responder_como_conta_inexistente_sem_contar_falha_quando_usuario_inativo(string senha)
    {
        Reflexao.Definir(Amb.Ana, nameof(Usuario.Ativo), false);

        for (var i = 0; i <= TentativasAntesDoBloqueio + 1; i++)
        {
            (await Handler.HandleAsync(new LoginCommand(EmailAna, senha), Ct)).DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo, ErrorType.NaoAutorizado);
        }

        Amb.Ana.FalhasLogin.ShouldBe(0);
        Amb.Ana.BloqueadoAte.ShouldBeNull();
        Amb.Auditoria.Acoes.ShouldAllBe(a => a == AcoesAuditoria.LoginInativo);
        Amb.Auditoria.Registros.Count.ShouldBe(TentativasAntesDoBloqueio + 2);
        Amb.Hasher.Verificacoes.ShouldBe(0);
        Amb.Hasher.VerificacoesSimuladas.ShouldBe(TentativasAntesDoBloqueio + 2);
        Amb.Banco.RefreshTokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deve_responder_credenciais_invalidas_e_nao_bloqueio_quando_conta_inativa_estava_bloqueada()
    {
        Reflexao.Definir(Amb.Ana, nameof(Usuario.Ativo), false);
        Reflexao.Definir(Amb.Ana, nameof(Usuario.BloqueadoAte), Amb.Agora.AddMinutes(10));

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct);

        resultado.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo);
    }

    [Fact]
    public async Task Deve_tratar_senha_nula_como_errada_quando_corpo_incompleto()
    {
        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, null!), Ct);
        var semEmail = await Handler.HandleAsync(new LoginCommand(null!, null!), Ct);

        resultado.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo);
        semEmail.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo);
    }

    [Fact]
    public async Task Deve_nao_logar_senha_nem_email_completo_quando_login_falha_ou_conclui()
    {
        await Handler.HandleAsync(new LoginCommand("desconhecido@apolice.dev", Ambiente.SenhaPadrao), Ct);
        await Handler.HandleAsync(new LoginCommand(EmailAna, "senha-errada-secreta"), Ct);
        var sucesso = await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct);

        var logs = string.Join('\n', Amb.Logs.Mensagens);
        logs.ShouldNotContain(Ambiente.SenhaPadrao);
        logs.ShouldNotContain("senha-errada-secreta");
        logs.ShouldNotContain("desconhecido@apolice.dev");
        logs.ShouldNotContain(EmailAna);
        logs.ShouldNotContain(sucesso.Value.RefreshToken);
        logs.ShouldContain("d***@apolice.dev");
        new LoginCommand("a@b.c", "x").ToString().ShouldNotContain("x }");
    }

    // Rajada de senhas erradas em paralelo: todas leem a mesma versão do usuário. A que perde o conflito precisa
    // contar a sua falha sobre o estado gravado pelas outras, senão o bloqueio da 5ª falha nunca chega.
    [Fact]
    public async Task Deve_contar_a_falha_sobre_o_estado_gravado_quando_tentativa_paralela_gravou_antes()
    {
        Amb.Banco.AgendarEscritaConcorrente(Amb.Ana, ana => RegistrarFalhas(ana, 3));
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);

        resultado.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo);
        Amb.Banco.TotalConflitos.ShouldBe(1);
        Amb.Ana.FalhasLogin.ShouldBe(4);
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.LoginFalhou]);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
        Amb.Hasher.Verificacoes.ShouldBe(1);
    }

    [Fact]
    public async Task Deve_bloquear_quando_a_falha_refeita_completa_o_limite_deixado_pelas_paralelas()
    {
        Amb.Banco.AgendarEscritaConcorrente(Amb.Ana, ana => RegistrarFalhas(ana, TentativasAntesDoBloqueio));

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);

        resultado.DeveFalharCom(AuthErros.UsuarioBloqueado.Codigo);
        Amb.Ana.EstaBloqueado(Amb.Agora).ShouldBeTrue();
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.UsuarioBloqueado]);
    }

    // Com o contador já em 1, o login correto altera o usuário (zera o contador) e entra na disputa de versão.
    [Fact]
    public async Task Deve_concluir_com_uma_unica_sessao_quando_o_login_correto_conflita_no_save()
    {
        await ErrarSenhaAsync(1);
        Amb.Banco.AgendarEscritaConcorrente(Amb.Ana, ana => RegistrarFalhas(ana, 2));

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct);

        var sessao = resultado.DeveTerSucesso();
        Amb.Banco.TotalConflitos.ShouldBe(1);
        Amb.Banco.RefreshTokens.ShouldHaveSingleItem().TokenHash.ShouldBe(RefreshToken.CalcularHash(sessao.RefreshToken));
        Amb.Ana.FalhasLogin.ShouldBe(0);
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.LoginFalhou, AcoesAuditoria.Login]);
    }

    [Fact]
    public async Task Deve_recusar_com_bloqueio_quando_a_conta_foi_bloqueada_durante_o_login()
    {
        await ErrarSenhaAsync(1);
        Amb.Banco.AgendarEscritaConcorrente(Amb.Ana, ana => RegistrarFalhas(ana, TentativasAntesDoBloqueio));

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, Ambiente.SenhaPadrao), Ct);

        resultado.DeveFalharCom(AuthErros.UsuarioBloqueado.Codigo);
        Amb.Banco.TotalConflitos.ShouldBe(1);
        Amb.Banco.RefreshTokens.ShouldBeEmpty();
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.LoginFalhou, AcoesAuditoria.LoginBloqueado]);
    }

    [Fact]
    public async Task Deve_responder_credenciais_invalidas_quando_o_usuario_some_durante_o_login()
    {
        Amb.Banco.AgendarEscritaConcorrente(Amb.Ana, ana => Amb.Banco.Usuarios.Remove(ana));

        var resultado = await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);

        resultado.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo);
        Amb.Auditoria.Registros.ShouldBeEmpty();
    }

    // Teto praticamente inalcançável (exige 10 gravações concorrentes no mesmo usuário), mas finito.
    [Fact]
    public async Task Deve_desistir_e_propagar_o_conflito_quando_ele_nao_cessa()
    {
        Amb.Banco.ForcarConflitos(10);

        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() => Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct));

        Amb.Banco.TotalConflitos.ShouldBe(10);
        Amb.Ana.FalhasLogin.ShouldBe(0);
        Amb.Auditoria.Registros.ShouldBeEmpty();
    }

    private static void RegistrarFalhas(Usuario usuario, int quantidade)
    {
        for (var i = 0; i < quantidade; i++)
        {
            usuario.RegistrarFalhaLogin(Ambiente.Inicio);
        }
    }

    private async Task ErrarSenhaAsync(int vezes)
    {
        for (var i = 0; i < vezes; i++)
        {
            await Handler.HandleAsync(new LoginCommand(EmailAna, "errada"), Ct);
        }
    }
}

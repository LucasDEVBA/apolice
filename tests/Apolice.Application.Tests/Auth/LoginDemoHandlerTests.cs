using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Features.Auth.LoginDemo;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Auth;

public sealed class LoginDemoHandlerTests : TesteBase
{
    private LoginDemoHandler Handler => Amb.Servico<LoginDemoHandler>();

    [Theory]
    [InlineData(PersonasDemo.Corretora, Papel.Corretor, "ana.corretora@apolice.dev")]
    [InlineData(PersonasDemo.Corretor, Papel.Corretor, "bruno.corretor@apolice.dev")]
    [InlineData(PersonasDemo.Analista, Papel.Analista, "carla.analista@apolice.dev")]
    [InlineData(" ADMIN ", Papel.Admin, "admin@apolice.dev")]
    public async Task Deve_entrar_como_usuario_do_seed_quando_persona_valida(string persona, Papel papel, string email)
    {
        var resultado = await Handler.HandleAsync(new LoginDemoCommand(persona), Ct);

        var usuario = resultado.DeveTerSucesso().Resposta.Usuario;
        usuario.Papel.ShouldBe(papel);
        usuario.Email.ShouldBe(email);
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.LoginDemo]);
        Amb.Banco.RefreshTokens.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Deve_retornar_desabilitada_quando_flag_login_demo_desligada()
    {
        Amb.Flags.Desligar(FeatureFlags.LoginDemo);

        var resultado = await Handler.HandleAsync(new LoginDemoCommand(PersonasDemo.Corretora), Ct);

        resultado.DeveFalharCom(ComumErros.FuncionalidadeDesabilitada.Codigo, ErrorType.NaoEncontrado);
        Amb.Banco.RefreshTokens.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("hacker")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Deve_retornar_persona_invalida_quando_persona_desconhecida(string? persona)
    {
        var resultado = await Handler.HandleAsync(new LoginDemoCommand(persona!), Ct);

        resultado.DeveFalharCom(AuthErros.PersonaInvalida.Codigo, ErrorType.Validacao);
    }

    [Fact]
    public async Task Deve_retornar_nao_encontrado_quando_seed_nao_criou_a_persona()
    {
        Amb.Banco.Usuarios.Remove(Amb.Bruno);

        var resultado = await Handler.HandleAsync(new LoginDemoCommand(PersonasDemo.Corretor), Ct);

        resultado.DeveFalharCom(ComumErros.NaoEncontrado(Entidades.Usuario).Codigo, ErrorType.NaoEncontrado);
    }

    [Fact]
    public async Task Deve_recusar_quando_persona_inativa()
    {
        Reflexao.Definir(Amb.Ana, nameof(Usuario.Ativo), false);

        var inativa = await Handler.HandleAsync(new LoginDemoCommand(PersonasDemo.Corretora), Ct);

        inativa.DeveFalharCom(AuthErros.CredenciaisInvalidas.Codigo);
        Amb.Banco.RefreshTokens.ShouldBeEmpty();
    }

    // Os e-mails das personas são públicos: o bloqueio por senha errada não pode tirar a demo do ar, e a demo não
    // pode zerar o contador de quem tenta adivinhar a senha.
    [Fact]
    public async Task Deve_entrar_sem_tocar_no_bloqueio_nem_no_contador_quando_persona_teve_senha_errada()
    {
        var login = Amb.Servico<LoginHandler>();
        for (var i = 0; i < 5; i++)
        {
            await login.HandleAsync(new LoginCommand("ana.corretora@apolice.dev", "errada"), Ct);
        }

        await login.HandleAsync(new LoginCommand("bruno.corretor@apolice.dev", "errada"), Ct);
        var bloqueadoAte = Amb.Ana.BloqueadoAte;

        var ana = await Handler.HandleAsync(new LoginDemoCommand(PersonasDemo.Corretora), Ct);
        var bruno = await Handler.HandleAsync(new LoginDemoCommand(PersonasDemo.Corretor), Ct);

        ana.DeveTerSucesso();
        bruno.DeveTerSucesso();
        Amb.Ana.EstaBloqueado(Amb.Agora).ShouldBeTrue();
        Amb.Ana.BloqueadoAte.ShouldBe(bloqueadoAte);
        Amb.Bruno.FalhasLogin.ShouldBe(1);
    }
}

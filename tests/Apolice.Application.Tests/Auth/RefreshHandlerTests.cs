using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Features.Auth.Logout;
using Apolice.Application.Features.Auth.Refresh;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Auth;

public sealed class RefreshHandlerTests : TesteBase
{
    private RefreshHandler Handler => Amb.Servico<RefreshHandler>();

    [Fact]
    public async Task Deve_rotacionar_o_token_quando_refresh_valido()
    {
        var login = await EntrarAsync();
        Amb.Avancar(TimeSpan.FromHours(1));

        var resultado = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);

        var nova = resultado.DeveTerSucesso();
        nova.RefreshToken.ShouldNotBe(login.RefreshToken);
        nova.Resposta.AccessToken.ShouldNotBe(login.Resposta.AccessToken);
        var antigo = Token(login.RefreshToken);
        var novo = Token(nova.RefreshToken);
        antigo.RevogadoEm.ShouldBe(Amb.Agora);
        antigo.SubstituidoPorId.ShouldBe(novo.Id);
        novo.FamiliaId.ShouldBe(antigo.FamiliaId);
        novo.EstaAtivo(Amb.Agora).ShouldBeTrue();
        nova.RefreshTokenExpiraEm.ShouldBe(Amb.Agora.AddDays(7));
    }

    [Fact]
    public async Task Deve_aceitar_o_token_rotacionado_quando_usado_em_seguida()
    {
        var login = await EntrarAsync();
        var primeira = (await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct)).DeveTerSucesso();

        var segunda = await Handler.HandleAsync(new RefreshCommand(primeira.RefreshToken), Ct);

        segunda.DeveTerSucesso();
        Amb.Banco.RefreshTokens.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Deve_retornar_refresh_invalido_quando_token_expirado()
    {
        var login = await EntrarAsync();
        Amb.Avancar(TimeSpan.FromDays(7));

        var resultado = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);

        resultado.DeveFalharCom(AuthErros.RefreshInvalido.Codigo, ErrorType.NaoAutorizado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-que-nunca-existiu")]
    public async Task Deve_retornar_refresh_invalido_quando_token_ausente_ou_desconhecido(string? token)
    {
        var resultado = await Handler.HandleAsync(new RefreshCommand(token), Ct);

        resultado.DeveFalharCom(AuthErros.RefreshInvalido.Codigo, ErrorType.NaoAutorizado);
    }

    [Fact]
    public async Task Deve_revogar_a_familia_inteira_quando_token_rotacionado_e_reutilizado()
    {
        var login = await EntrarAsync();
        var rotacionada = (await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct)).DeveTerSucesso();
        var outraSessao = await EntrarAsync();

        var reuso = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);
        var depois = await Handler.HandleAsync(new RefreshCommand(rotacionada.RefreshToken), Ct);

        reuso.DeveFalharCom(AuthErros.RefreshReutilizado.Codigo, ErrorType.NaoAutorizado);
        depois.DeveFalharCom(AuthErros.RefreshInvalido.Codigo);
        var familia = Token(login.RefreshToken).FamiliaId;
        Amb.Banco.RefreshTokens.Where(t => t.FamiliaId == familia).ShouldAllBe(t => t.EstaRevogado);
        Token(outraSessao.RefreshToken).EstaRevogado.ShouldBeFalse();
        Amb.Auditoria.Registros.Single(r => r.Acao == AcoesAuditoria.RefreshReutilizado).Detalhes.ShouldBe($$"""{"familiaId":"{{familia}}"}""");
    }

    [Fact]
    public async Task Deve_revogar_auditar_e_recusar_quando_usuario_desativado()
    {
        var login = await EntrarAsync();
        Reflexao.Definir(Amb.Ana, nameof(Usuario.Ativo), false);
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);

        resultado.DeveFalharCom(AuthErros.RefreshInvalido.Codigo);
        Amb.Banco.RefreshTokens.ShouldAllBe(t => t.EstaRevogado);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
        var auditoria = Amb.Auditoria.Registros.Last();
        auditoria.Acao.ShouldBe(AcoesAuditoria.SessaoRevogadaUsuarioInativo);
        auditoria.EntidadeId.ShouldBe(Amb.Ana.Id.ToString());
        auditoria.Detalhes.ShouldBe($$"""{"familiaId":"{{Token(login.RefreshToken).FamiliaId}}"}""");
    }

    [Fact]
    public async Task Deve_revogar_e_recusar_quando_usuario_foi_removido()
    {
        var login = await EntrarAsync();
        Amb.Banco.Usuarios.Remove(Amb.Ana);

        var resultado = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);

        resultado.DeveFalharCom(AuthErros.RefreshInvalido.Codigo);
        Token(login.RefreshToken).EstaRevogado.ShouldBeTrue();
        Amb.Auditoria.Acoes.Last().ShouldBe(AcoesAuditoria.SessaoRevogadaUsuarioInativo);
    }

    [Fact]
    public async Task Deve_manter_a_sessao_quando_usuario_esta_bloqueado_por_senha_errada()
    {
        var login = await EntrarAsync();
        Reflexao.Definir(Amb.Ana, nameof(Usuario.BloqueadoAte), Amb.Agora.AddMinutes(10));

        var resultado = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);

        resultado.DeveTerSucesso();
    }

    [Fact]
    public async Task Deve_revogar_a_familia_e_ser_idempotente_quando_logout()
    {
        var login = await EntrarAsync();
        var rotacionada = (await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct)).DeveTerSucesso();
        var logout = Amb.Servico<LogoutHandler>();

        var primeiro = await logout.HandleAsync(new LogoutCommand(rotacionada.RefreshToken), Ct);
        var repetido = await logout.HandleAsync(new LogoutCommand(rotacionada.RefreshToken), Ct);
        var semCookie = await logout.HandleAsync(new LogoutCommand(null), Ct);
        var desconhecido = await logout.HandleAsync(new LogoutCommand("nao-existe"), Ct);

        primeiro.DeveTerSucesso();
        repetido.DeveTerSucesso();
        semCookie.DeveTerSucesso();
        desconhecido.DeveTerSucesso();
        Amb.Banco.RefreshTokens.ShouldAllBe(t => t.EstaRevogado);
        Amb.Auditoria.Acoes.Count(a => a == AcoesAuditoria.Logout).ShouldBe(2);
        new RefreshCommand("segredo").ToString().ShouldNotContain("segredo");
        new LogoutCommand("segredo").ToString().ShouldNotContain("segredo");
    }

    // Só o token vigente da família está ativo: revogar apenas o token apresentado (já rotacionado) deixaria a sessão viva.
    [Fact]
    public async Task Deve_revogar_o_token_vigente_quando_logout_usa_um_token_antigo_da_familia()
    {
        var login = await EntrarAsync();
        var vigente = (await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct)).DeveTerSucesso();

        (await Amb.Servico<LogoutHandler>().HandleAsync(new LogoutCommand(login.RefreshToken), Ct)).DeveTerSucesso();

        Token(vigente.RefreshToken).EstaRevogado.ShouldBeTrue();
        (await Handler.HandleAsync(new RefreshCommand(vigente.RefreshToken), Ct)).DeveFalharCom(AuthErros.RefreshInvalido.Codigo);
    }

    // Refinamento do SPEC 5: token revogado sem substituto (logout) não prova roubo — sem alarme nem auditoria de reuso.
    [Fact]
    public async Task Deve_recusar_sem_alarme_de_reuso_quando_cookie_volta_depois_do_logout()
    {
        var login = await EntrarAsync();
        await Amb.Servico<LogoutHandler>().HandleAsync(new LogoutCommand(login.RefreshToken), Ct);
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct);

        resultado.DeveFalharCom(AuthErros.RefreshInvalido.Codigo, ErrorType.NaoAutorizado);
        Amb.Auditoria.Acoes.ShouldNotContain(AcoesAuditoria.RefreshReutilizado);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Logs.Mensagens.ShouldNotContain(m => m.Contains("Reuso", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deve_propagar_o_conflito_sem_rotacionar_quando_dois_refresh_concorrem()
    {
        var login = await EntrarAsync();
        Amb.Banco.FalharProximoSaveComConflito = true;

        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() => Handler.HandleAsync(new RefreshCommand(login.RefreshToken), Ct));

        var token = Amb.Banco.RefreshTokens.ShouldHaveSingleItem();
        token.EstaAtivo(Amb.Agora).ShouldBeTrue();
        token.SubstituidoPorId.ShouldBeNull();
    }

    private RefreshToken Token(string texto) => Amb.Banco.RefreshTokens.Single(t => t.TokenHash == RefreshToken.CalcularHash(texto));

    private async Task<ResultadoAutenticacao> EntrarAsync() =>
        (await Amb.Servico<LoginHandler>().HandleAsync(new LoginCommand("ana.corretora@apolice.dev", Ambiente.SenhaPadrao), Ct)).DeveTerSucesso();
}

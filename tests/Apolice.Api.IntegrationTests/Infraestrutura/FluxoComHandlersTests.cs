using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Apolices.GerarDocumento;
using Apolice.Application.Features.Apolices.Obter;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Features.Auth.Refresh;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Criar;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.IniciarAnalise;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using Apolice.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

// Os handlers reais da Application sobre a infraestrutura real: cotação → emissão → endosso → sinistro → aprovação,
// com o relay do outbox entregando aos consumidores (notificações e triagem) e o login/refresh persistindo sessão.
public sealed class FluxoComHandlersTests(PostgresFixture postgres) : TesteComBanco(postgres, "fluxo")
{
    [Fact]
    public async Task Deve_percorrer_o_ciclo_de_vida_da_apolice_quando_os_handlers_usam_a_infra_real()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var carla = await UsuarioAsync(PersonasDemo.Analista);
        Ambiente.Usuario.Definir(ana);
        var hoje = Relogio.HojeEm(Ambiente.Tempo.GetUtcNow());

        var cotacao = await Executar<CriarCotacaoHandler, CotacaoDto>(h => h.HandleAsync(new CriarCotacaoCommand(PedidoResidencial()), Ct));
        await Executar<AceitarCotacaoHandler, CotacaoDto>(h => h.HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct));
        var apolice = await Executar<EmitirApoliceHandler, ApoliceDto>(h => h.HandleAsync(new EmitirApoliceCommand(cotacao.Id, hoje.AddDays(1), 3), Ct));
        apolice.Numero.ShouldStartWith($"RESID-{hoje.Year}-");
        apolice.Status.ShouldBe(StatusEfetivoApolice.Programada);

        Ambiente.Tempo.Advance(TimeSpan.FromDays(10));
        var endossada = await Executar<EndossarApoliceHandler, ApoliceDto>(h =>
            h.HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Roubo, 20_000m), Ct));
        endossada.Endossos.ShouldHaveSingleItem().CriadoPorNome.ShouldBe("Ana Ribeiro");

        var hojeSinistro = Relogio.HojeEm(Ambiente.Tempo.GetUtcNow());
        var sinistro = await Executar<AbrirSinistroHandler, Application.Features.Sinistros.SinistroDto>(h => h.HandleAsync(
            new AbrirSinistroCommand(apolice.Id, CodigosCobertura.Incendio, hojeSinistro.AddDays(-1), "Curto-circuito na cozinha provocou incêndio no armário.", 18_000m),
            Ct));
        sinistro.Numero.ShouldStartWith($"SIN-{hojeSinistro.Year}-");

        Ambiente.Usuario.Definir(carla);
        await Executar<IniciarAnaliseSinistroHandler, Application.Features.Sinistros.SinistroDto>(h => h.HandleAsync(new IniciarAnaliseSinistroCommand(sinistro.Id), Ct));
        var aprovado = await Executar<AprovarSinistroHandler, Application.Features.Sinistros.SinistroDto>(h => h.HandleAsync(new AprovarSinistroCommand(sinistro.Id, 15_000m, "Laudo confirma o dano."), Ct));
        aprovado.Status.ShouldBe(StatusSinistro.Aprovado);
        aprovado.Historico.Select(h => h.UsuarioNome).ShouldBe(["Ana Ribeiro", "Carla Souza", "Carla Souza"]);

        // Relay → consumidores reais: boas-vindas, endosso, sinistro aberto (com triagem) e sinistro decidido.
        await Ambiente.ExecutarAsync<OutboxDispatcher, int>(d => d.DespacharPendentesAsync(50, Ct));
        var detalhe = await Executar<ObterApoliceHandler, ApoliceDto>(h => h.HandleAsync(new ObterApoliceQuery(apolice.Id), Ct));
        detalhe.Notificacoes.Select(n => n.Tipo).ShouldBe(
            [TipoNotificacao.BoasVindas, TipoNotificacao.Endosso, TipoNotificacao.SinistroAberto, TipoNotificacao.SinistroDecidido],
            ignoreOrder: true);
        detalhe.BoasVindasEnviadasEm.ShouldNotBeNull();
        detalhe.Sinistros.ShouldHaveSingleItem().TriagemScore.ShouldNotBeNull();

        var documento = await Executar<GerarDocumentoApoliceHandler, DocumentoApoliceDto>(h => h.HandleAsync(new GerarDocumentoApoliceQuery(apolice.Id), Ct));
        Documentos.DeveParecerPdf(documento.Conteudo, "apolice-fluxo.pdf");
    }

    [Fact]
    public async Task Deve_autenticar_com_a_senha_do_seed_e_rotacionar_o_refresh_token()
    {
        Ambiente.Usuario.Anonimo();
        var login = await Executar<LoginHandler, ResultadoAutenticacao>(h => h.HandleAsync(new LoginCommand("ana.corretora@apolice.dev", AmbienteInfra.SenhaSeed), Ct));
        login.Resposta.Usuario.Nome.ShouldBe("Ana Ribeiro");

        var renovado = await Executar<RefreshHandler, ResultadoAutenticacao>(h => h.HandleAsync(new RefreshCommand(login.RefreshToken), Ct));
        renovado.RefreshToken.ShouldNotBe(login.RefreshToken);

        await NoEscopoAsync(async sp =>
        {
            var reuso = await sp.GetRequiredService<RefreshHandler>().HandleAsync(new RefreshCommand(login.RefreshToken), Ct);
            reuso.IsFailure.ShouldBeTrue();
            reuso.Error.Codigo.ShouldBe("Auth.RefreshReutilizado");
        });

        var senhaErrada = await NoEscopoAsync(sp => sp.GetRequiredService<LoginHandler>().HandleAsync(new LoginCommand("ana.corretora@apolice.dev", "senha-errada"), Ct));
        senhaErrada.Error.Codigo.ShouldBe("Auth.CredenciaisInvalidas");
    }

    private static CotacaoRequest PedidoResidencial() =>
        new(
            CodigosProduto.Residencial,
            new SeguradoRequest("Helena Duarte Pires", Cenarios.CpfMaria, "helena.pires@exemplo.test", new DateOnly(1990, 7, 21)),
            new DadosRiscoResidencial(500_000m, TipoImovel.Casa, "40140110", Habitual: true),
            [new CoberturaRequest(CodigosCobertura.Incendio, 500_000m), new CoberturaRequest(CodigosCobertura.DanosEletricos, 10_000m)]);

    private async Task<T> Executar<THandler, T>(Func<THandler, Task<Result<T>>> acao)
        where THandler : notnull
    {
        var resultado = await NoEscopoAsync(sp => acao(sp.GetRequiredService<THandler>()));
        resultado.IsSuccess.ShouldBeTrue(resultado.IsFailure ? $"{resultado.Error.Codigo}: {resultado.Error.Mensagem}" : null);
        return resultado.Value;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Apolice.Api.Http;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Sinistros;
using Apolice.Infrastructure.Idempotencia;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Api;

// Idempotency-Key (SPEC 7): repetir a mesma chave com o mesmo corpo devolve a resposta original sem reexecutar;
// com outro corpo, 422; enquanto a primeira ainda executa, 409. A chave é por usuário.
public sealed class IdempotenciaApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_idempotencia")
{
    [Fact]
    public async Task Deve_devolver_a_mesma_apolice_quando_a_emissao_e_repetida_com_a_mesma_chave()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var cotacao = await Operacoes.CotacaoAceitaAsync(ana);
        var chave = Guid.NewGuid().ToString();

        using var primeira = await Operacoes.EmitirAsync(ana, Operacoes.Emissao(cotacao.Id), chave);
        using var repetida = await Operacoes.EmitirAsync(ana, Operacoes.Emissao(cotacao.Id), chave);

        var original = await primeira.LerAsync<ApoliceDto>(HttpStatusCode.Created);
        var replay = await repetida.LerAsync<ApoliceDto>(HttpStatusCode.Created);
        replay.Id.ShouldBe(original.Id);
        replay.Numero.ShouldBe(original.Numero);
        repetida.Headers.GetValues("Idempotent-Replayed").Single().ShouldBe("true");
        repetida.Headers.Location!.OriginalString.ShouldBe($"/api/v1/apolices/{original.Id}");

        using var lista = await ana.GetAsync("/api/v1/apolices", Ct);
        (await lista.LerAsync<Pagina<ApoliceResumoDto>>(HttpStatusCode.OK)).Itens.Count(a => a.Id == original.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Deve_responder_422_quando_a_chave_e_reutilizada_com_outro_corpo()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var chave = Guid.NewGuid().ToString();

        using var primeira = await Operacoes.AbrirSinistroAsync(ana, Operacoes.Sinistro(apolice.Id), chave);
        using var outroCorpo = await Operacoes.AbrirSinistroAsync(ana, Operacoes.Sinistro(apolice.Id, valor: 7_000m), chave);
        using var mesmoCorpo = await Operacoes.AbrirSinistroAsync(ana, Operacoes.Sinistro(apolice.Id), chave);

        var aberto = await primeira.LerAsync<SinistroDto>(HttpStatusCode.Created);
        await outroCorpo.ProblemaAsync(HttpStatusCode.UnprocessableEntity, "Idempotencia.CorpoDiferente");
        (await mesmoCorpo.LerAsync<SinistroDto>(HttpStatusCode.Created)).Id.ShouldBe(aberto.Id);

        using var lista = await ana.GetAsync("/api/v1/sinistros", Ct);
        (await lista.LerAsync<Pagina<SinistroResumoDto>>(HttpStatusCode.OK)).Itens.ShouldHaveSingleItem().Id.ShouldBe(aberto.Id);
    }

    [Fact]
    public async Task Deve_executar_de_novo_quando_a_mesma_chave_vem_de_outro_usuario()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        using var admin = await ComoAsync(PersonasDemo.Admin);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var chave = Guid.NewGuid().ToString();

        using var deAna = await Operacoes.AbrirSinistroAsync(ana, Operacoes.Sinistro(apolice.Id), chave);
        using var deAdmin = await Operacoes.AbrirSinistroAsync(admin, Operacoes.Sinistro(apolice.Id), chave);

        var primeiro = await deAna.LerAsync<SinistroDto>(HttpStatusCode.Created);
        (await deAdmin.LerAsync<SinistroDto>(HttpStatusCode.Created)).Id.ShouldNotBe(primeiro.Id);
    }

    [Theory]
    [InlineData("com espaco")]
    [InlineData("acentuação")]
    public async Task Deve_responder_400_quando_a_chave_e_invalida(string chave)
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var cotacao = await Operacoes.CotacaoAceitaAsync(ana);

        using var resposta = await Operacoes.EmitirAsync(ana, Operacoes.Emissao(cotacao.Id), chave);

        await resposta.ProblemaAsync(HttpStatusCode.BadRequest, "Idempotencia.ChaveInvalida");
    }

    [Fact]
    public async Task Deve_responder_400_quando_a_chave_passa_de_100_caracteres()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var cotacao = await Operacoes.CotacaoAceitaAsync(ana);

        using var resposta = await Operacoes.EmitirAsync(ana, Operacoes.Emissao(cotacao.Id), new string('k', 101));

        await resposta.ProblemaAsync(HttpStatusCode.BadRequest, "Idempotencia.ChaveInvalida");
    }

    [Fact]
    public async Task Deve_criar_um_unico_sinistro_quando_a_mesma_chave_chega_em_requisicoes_simultaneas()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var chave = Guid.NewGuid().ToString();
        const int Simultaneas = 6;

        var respostas = await Task.WhenAll(Enumerable.Range(0, Simultaneas).Select(_ => Operacoes.AbrirSinistroAsync(ana, Operacoes.Sinistro(apolice.Id), chave)));

        try
        {
            respostas.Select(r => r.StatusCode).ShouldAllBe(s => s == HttpStatusCode.Created || s == HttpStatusCode.Conflict);
            respostas.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBeGreaterThanOrEqualTo(1);
            using var lista = await ana.GetAsync("/api/v1/sinistros", Ct);
            (await lista.LerAsync<Pagina<SinistroResumoDto>>(HttpStatusCode.OK)).Itens.Count.ShouldBe(1);
        }
        finally
        {
            foreach (var resposta in respostas)
            {
                resposta.Dispose();
            }
        }
    }

    [Fact]
    public async Task Deve_responder_409_com_retry_after_enquanto_a_mesma_chave_esta_em_processamento()
    {
        using var ana = Anonimo();
        var sessao = await EntrarAsync(ana, PersonasDemo.Corretora);
        ana.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var pedido = Operacoes.Sinistro(apolice.Id);
        var chave = Guid.NewGuid().ToString();
        var armazem = Api.Services.GetRequiredService<IArmazemIdempotencia>();
        var hash = Api.Services.GetRequiredService<HashRequisicao>().Calcular(pedido);
        (await armazem.ReservarAsync(sessao.Usuario.Id, chave, hash, DateTimeOffset.UtcNow, Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);

        using var emProcessamento = await Operacoes.AbrirSinistroAsync(ana, pedido, chave);
        await emProcessamento.ProblemaAsync(HttpStatusCode.Conflict, "Idempotencia.EmProcessamento");
        emProcessamento.Headers.GetValues("Retry-After").Single().ShouldBe("1");

        await armazem.LiberarAsync(sessao.Usuario.Id, chave, Ct);
        using var liberada = await Operacoes.AbrirSinistroAsync(ana, pedido, chave);
        await liberada.DeveTerStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Deve_tratar_como_o_mesmo_corpo_quando_so_a_escala_do_decimal_muda()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var apolice = await Operacoes.ApoliceAsync(ana);
        var chave = Guid.NewGuid().ToString();
        var hoje = Pedidos.Hoje.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        string Corpo(string valor) =>
            $$"""{"apoliceId":"{{apolice.Id}}","coberturaCodigo":"CASCO","dataOcorrencia":"{{hoje}}","descricao":"Colisão traseira no semáforo com dano no para-choque.","valorReclamado":{{valor}}}""";

        using var primeira = await EnviarAsync(ana, Corpo("6500"), chave);
        using var repetida = await EnviarAsync(ana, Corpo("6500.00"), chave);

        var aberto = await primeira.LerAsync<SinistroDto>(HttpStatusCode.Created);
        (await repetida.LerAsync<SinistroDto>(HttpStatusCode.Created)).Id.ShouldBe(aberto.Id);
        repetida.Headers.GetValues("Idempotent-Replayed").Single().ShouldBe("true");
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string corpo, string chave)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sinistros") { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        requisicao.Headers.TryAddWithoutValidation("Idempotency-Key", chave).ShouldBeTrue();
        return await cliente.SendAsync(requisicao, Ct);
    }
}

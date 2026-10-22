using System.Net;
using System.Net.Http.Json;
using System.Text;
using Apolice.Api.IntegrationTests.Api.Suporte;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes;
using Apolice.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Api;

// Result/Error e exceções → HTTP (SPEC 7): 400 de validação (campos em camelCase) e de JSON malformado, 409 de
// concorrência, 422 de regra de negócio e 500 sem detalhe interno — todos como ProblemDetails do contrato.
public sealed class ErrosApiTests(PostgresFixture postgres) : ApiTeste(postgres, "api_erros")
{
    private readonly GatilhoFalhaUnitOfWork _gatilho = new();

    [Fact]
    public async Task Deve_responder_400_com_campos_em_camel_case_quando_o_corpo_e_invalido()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var pedido = Pedidos.CotacaoAuto(cpf: "11111111111") with
        {
            Coberturas = [new CoberturaRequest("CASCO", null), new CoberturaRequest("RCF_DM", -1m)],
        };

        using var resposta = await ana.PostAsJsonAsync("/api/v1/cotacoes", pedido, Json, Ct);

        var problema = await resposta.ProblemaAsync(HttpStatusCode.BadRequest, "Comum.Validacao");
        var erros = problema.GetProperty("errors");
        erros.GetProperty("segurado.cpf")[0].GetString().ShouldBe("CPF inválido.");
        erros.GetProperty("coberturas[1].lmi")[0].GetString().ShouldBe("O LMI deve ser maior que zero.");
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("""{"produtoCodigo":"AUTO","segurado":null,"dadosRisco":{"produto":"BARCO"},"coberturas":[]}""")]
    [InlineData("""{"produtoCodigo":"VIDA","segurado":null,"dadosRisco":{"fumante":true},"coberturas":[]}""")]
    [InlineData("""{"produtoCodigo":"VIDA","segurado":null,"dadosRisco":{"produto":"vida","fumante":true},"coberturas":[]}""")]
    [InlineData("""{"cotacaoId":"0199f0c4-0000-7000-8000-000000000000","inicioVigencia":"2026-10-22","parcelas":"4"}""")]
    public async Task Deve_responder_400_quando_o_json_nao_segue_o_contrato(string corpo)
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        string[] rotas = corpo.Contains("cotacaoId", StringComparison.Ordinal) ? ["/api/v1/apolices"] : ["/api/v1/cotacoes", "/api/v1/cotacoes/simular"];

        foreach (var rota in rotas)
        {
            using var resposta = await ana.PostAsync(rota, new StringContent(corpo, Encoding.UTF8, "application/json"), Ct);

            await resposta.ProblemaAsync(HttpStatusCode.BadRequest, "Comum.RequisicaoInvalida");
        }
    }

    [Theory]
    [InlineData("/api/v1/cotacoes?limite=500", "limite")]
    [InlineData("/api/v1/sinistros?cursor=!!!", "cursor")]
    public async Task Deve_responder_400_quando_a_query_e_invalida(string rota, string campo)
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);

        using var resposta = await ana.GetAsync(rota, Ct);

        var problema = await resposta.ProblemaAsync(HttpStatusCode.BadRequest, "Comum.Validacao");
        problema.GetProperty("errors").TryGetProperty(campo, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Deve_responder_422_quando_a_regra_de_negocio_recusa_a_transicao()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var cotacao = await Operacoes.CotacaoAceitaAsync(ana);

        using var resposta = await ana.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/aceitar", null, Ct);

        await resposta.ProblemaAsync(HttpStatusCode.UnprocessableEntity, "Cotacao.StatusInvalido");
    }

    [Fact]
    public async Task Deve_responder_409_quando_o_salvamento_sofre_conflito_de_concorrencia()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var cotacao = await Operacoes.CriarCotacaoAsync(ana);
        _gatilho.Armar(new ConflitoDeConcorrenciaException());

        using var resposta = await ana.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/aceitar", null, Ct);

        await resposta.ProblemaAsync(HttpStatusCode.Conflict, "Concorrencia.Conflito");
    }

    [Fact]
    public async Task Deve_responder_500_sem_detalhes_internos_quando_ocorre_erro_inesperado()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);
        var cotacao = await Operacoes.CriarCotacaoAsync(ana);
        _gatilho.Armar(new InvalidOperationException("segredo-interno: Host=db;Password=nao-vaza"));

        using var resposta = await ana.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/aceitar", null, Ct);

        await resposta.ProblemaAsync(HttpStatusCode.InternalServerError, "Comum.ErroInterno");
        var corpo = await resposta.Content.ReadAsStringAsync(Ct);
        corpo.ShouldNotContain("segredo-interno");
        corpo.ShouldNotContain("InvalidOperationException");
        corpo.ShouldNotContain(" at ");
    }

    [Fact]
    public async Task Deve_responder_404_em_problem_details_quando_a_rota_nao_existe()
    {
        using var ana = await ComoAsync(PersonasDemo.Corretora);

        using var resposta = await ana.GetAsync("/api/v1/nao-existe", Ct);

        await resposta.ProblemaAsync(HttpStatusCode.NotFound, "Comum.NaoEncontrado");
    }

    protected override void Servicos(IServiceCollection servicos)
    {
        servicos.AddSingleton(_gatilho);
        servicos.AddScoped<IUnitOfWork>(sp => new UnitOfWorkComFalha(sp.GetRequiredService<ApoliceDbContext>(), sp.GetRequiredService<GatilhoFalhaUnitOfWork>()));
    }

    // Falha determinística no próximo SaveChanges: a corrida real é não determinística, e o que se testa aqui é o
    // mapeamento da exceção para HTTP (a detecção do conflito em si está nos testes da infraestrutura).
    public sealed class GatilhoFalhaUnitOfWork
    {
        private Exception? _proxima;

        public void Armar(Exception excecao) => _proxima = excecao;

        public Exception? Consumir() => Interlocked.Exchange(ref _proxima, null);
    }

    private sealed class UnitOfWorkComFalha(ApoliceDbContext contexto, GatilhoFalhaUnitOfWork gatilho) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            gatilho.Consumir() is { } falha ? throw falha : contexto.SaveChangesAsync(cancellationToken);

        public void DescartarAlteracoes() => ((IUnitOfWork)contexto).DescartarAlteracoes();
    }
}

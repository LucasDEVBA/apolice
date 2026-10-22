using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Cotacoes.Criar;
using Apolice.Application.Features.Cotacoes.Simular;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Tests.Cotacoes;

public sealed class CriarCotacaoHandlerTests : TesteBase
{
    [Fact]
    public async Task Deve_gravar_auditar_e_devolver_o_dto_relido_quando_corretor_cria()
    {
        var savesAntes = Amb.Banco.TotalSaves;

        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);

        var gravada = Amb.Banco.Cotacoes.ShouldHaveSingleItem();
        cotacao.Id.ShouldBe(gravada.Id);
        cotacao.CorretorId.ShouldBe(Amb.Ana.Id);
        cotacao.CorretorNome.ShouldBe("Ana Ribeiro");
        cotacao.ProdutoNome.ShouldBe("Seguro Auto");
        cotacao.Status.ShouldBe(StatusEfetivoCotacao.Calculada);
        cotacao.ValidaAte.ShouldBe(Amb.Agora.AddDays(30));
        cotacao.PremioTotal.ShouldBe(Dados.PremioTotalAuto);
        cotacao.Segurado.Cpf.ShouldBe(Dados.CpfValido);
        cotacao.DadosRisco.ShouldBeOfType<DadosRiscoAuto>();
        cotacao.ApoliceId.ShouldBeNull();
        var auditoria = Amb.Auditoria.Registros.ShouldHaveSingleItem();
        auditoria.Acao.ShouldBe(AcoesAuditoria.CotacaoCriada);
        auditoria.Detalhes.DeveSerJsonEquivalente("""{"produto":"AUTO","premioTotal":4080.44}""");
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
    }

    [Fact]
    public async Task Deve_criar_na_carteira_do_admin_quando_admin_cota()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Diego, Dados.CotacaoVida());

        cotacao.CorretorId.ShouldBe(Amb.Diego.Id);
        cotacao.ProdutoCodigo.ShouldBe(CodigosProduto.Vida);
    }

    // Uma regra só de normalização (a mesma do endosso, do sinistro e das rotas): "auto"/"casco" valem como AUTO/CASCO.
    [Fact]
    public async Task Deve_aceitar_codigos_em_minusculas_quando_corretor_cria()
    {
        var request = Dados.CotacaoAuto() with { ProdutoCodigo = " auto ", Coberturas = [new("casco", null), new(" rcf_dm ", 100_000m)] };

        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana, request);

        cotacao.ProdutoCodigo.ShouldBe(CodigosProduto.Auto);
        cotacao.Itens.Select(i => i.Codigo).ShouldBe([CodigosCobertura.Casco, CodigosCobertura.RcfDanosMateriais]);
        cotacao.PremioTotal.ShouldBe(Dados.PremioTotalAuto);
    }

    // Taxa alterada por outra instância da API: a simulação (cache) ainda mostra a antiga, mas o prêmio GRAVADO sai do
    // cadastro. CASCO de 3,2% para 4%: 100.000 × 0,04 = 4.000 + RCF_DM 600 = 4.600 líquido.
    [Fact]
    public async Task Deve_gravar_o_premio_pelo_cadastro_e_nao_pelo_cache_quando_cache_esta_defasado()
    {
        Amb.Catalogo.Aquecer();
        Amb.Banco.Produtos.Single(p => p.Codigo == CodigosProduto.Auto).AtualizarTaxa(CodigosCobertura.Casco, 0.04m, Amb.Agora).IsSuccess.ShouldBeTrue();

        var simulada = (await Amb.Servico<SimularCotacaoHandler>().HandleAsync(new SimularCotacaoCommand(Dados.CotacaoAuto()), Ct)).DeveTerSucesso();
        var criada = await Amb.CriarCotacaoAsync(Amb.Ana);

        simulada.PremioLiquido.ShouldBe(Dados.PremioLiquidoAuto);
        criada.PremioLiquido.ShouldBe(4_600m);
    }

    [Fact]
    public async Task Deve_nao_gravar_quando_tarifacao_falha_ou_papel_nao_permite()
    {
        Amb.Como(Amb.Ana);
        var invalida = await Amb.Servico<CriarCotacaoHandler>().HandleAsync(new CriarCotacaoCommand(Dados.CotacaoAuto() with { Coberturas = [] }), Ct);
        Amb.Como(Amb.Carla);
        var analista = await Amb.Servico<CriarCotacaoHandler>().HandleAsync(new CriarCotacaoCommand(Dados.CotacaoAuto()), Ct);

        invalida.DeveFalharCom("Tarifacao.CoberturaObrigatoriaAusente");
        analista.DeveFalharCom("Comum.Proibido");
        Amb.Banco.Cotacoes.ShouldBeEmpty();
        Amb.Auditoria.Registros.ShouldBeEmpty();
    }
}

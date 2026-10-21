using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Produtos;

public sealed class ProdutoTests
{
    [Fact]
    public void Deve_atualizar_taxa_e_levantar_evento_quando_cobertura_por_taxa()
    {
        var produto = CatalogoPadrao.Auto();

        produto.AtualizarTaxa(CodigosCobertura.RcfDanosMateriais, 0.0070m, Cenarios.Agora).DeveTerSucesso();

        produto.ObterCobertura("RCF_DM")!.Taxa.ShouldBe(0.0070m);
        var evento = produto.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TaxaCoberturaAtualizada>();
        evento.ProdutoCodigo.ShouldBe("AUTO");
        evento.CoberturaCodigo.ShouldBe("RCF_DM");
        evento.TaxaAnterior.ShouldBe(0.0060m);
        evento.TaxaNova.ShouldBe(0.0070m);
        evento.OcorridoEm.ShouldBe(Cenarios.Agora);
    }

    [Fact]
    public void Deve_ignorar_sem_evento_quando_taxa_nova_igual_a_atual()
    {
        var produto = CatalogoPadrao.Auto();

        produto.AtualizarTaxa(CodigosCobertura.RcfDanosMateriais, 0.0060m, Cenarios.Agora).DeveTerSucesso();

        produto.DomainEvents.ShouldBeEmpty();
        produto.ObterCobertura("RCF_DM")!.Taxa.ShouldBe(0.0060m);
    }

    [Fact]
    public void Deve_isolar_as_coberturas_quando_mesmas_instancias_criam_dois_produtos()
    {
        CoberturaProduto[] compartilhadas = [CoberturaComTaxa("A")];
        var primeiro = Produto.Criar("AUTO", "Auto 1", "Descrição", 0.0738m, compartilhadas).Value;
        var segundo = Produto.Criar("AUTO", "Auto 2", "Descrição", 0.0738m, compartilhadas).Value;

        primeiro.AtualizarTaxa("A", 0.02m, Cenarios.Agora).DeveTerSucesso();

        segundo.ObterCobertura("A")!.Taxa.ShouldBe(0.01m);
        compartilhadas[0].Taxa.ShouldBe(0.01m);
        primeiro.ObterCobertura("A").ShouldNotBeSameAs(compartilhadas[0]);
    }

    [Fact]
    public void Deve_aceitar_taxa_igual_a_1_quando_limite_superior()
    {
        CatalogoPadrao.Vida().AtualizarTaxa(CodigosCobertura.Morte, 1m, Cenarios.Agora).DeveTerSucesso();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.0001")]
    public void Deve_rejeitar_quando_taxa_fora_de_zero_exclusivo_a_um(string taxa)
    {
        var produto = CatalogoPadrao.Auto();

        produto.AtualizarTaxa("CASCO", Valor.De(taxa), Cenarios.Agora)
            .DeveFalharCom("Produto.TaxaInvalida", ErrorType.Validacao);
        produto.DomainEvents.ShouldBeEmpty();
        produto.ObterCobertura("CASCO")!.Taxa.ShouldBe(0.0320m);
    }

    [Fact]
    public void Deve_rejeitar_quando_cobertura_de_premio_fixo()
    {
        CatalogoPadrao.Auto().AtualizarTaxa(CodigosCobertura.Vidros, 0.01m, Cenarios.Agora)
            .DeveFalharCom("Produto.CoberturaDePremioFixo", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public void Deve_retornar_nao_encontrado_quando_cobertura_inexistente()
    {
        CatalogoPadrao.Auto().AtualizarTaxa("XYZ", 0.01m, Cenarios.Agora)
            .DeveFalharCom("Produto.CoberturaNaoEncontrada", ErrorType.NaoEncontrado);
    }

    [Fact]
    public void Deve_usar_o_codigo_como_id_quando_produto_carregado()
    {
        var produto = CatalogoPadrao.Residencial();

        produto.Id.ShouldBe("RESID");
        produto.Codigo.ShouldBe(produto.Id);
        produto.ObterCobertura("NAO_EXISTE").ShouldBeNull();
        produto.ObterCobertura(null).ShouldBeNull();
    }

    [Fact]
    public void Deve_criar_quando_dados_validos()
    {
        var produto = Produto.Criar("VIDA", "Vida", "Descrição", 0.0038m, [CoberturaComTaxa("MORTE")]).DeveTerSucesso();

        produto.Coberturas.ShouldHaveSingleItem().Codigo.ShouldBe("MORTE");
    }

    [Fact]
    public void Deve_reconhecer_apenas_auto_resid_e_vida_quando_valida_codigo()
    {
        CodigosProduto.Todos.ShouldBe(["AUTO", "RESID", "VIDA"]);
        CodigosProduto.EhValido("VIDA").ShouldBeTrue();
        CodigosProduto.EhValido(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData("MOTO", "Produto.CodigoInvalido")]
    [InlineData("auto", "Produto.CodigoInvalido")]
    public void Deve_rejeitar_quando_codigo_fora_do_catalogo(string codigo, string erro)
    {
        Produto.Criar(codigo, "Nome", "Descrição", 0.0738m, [CoberturaComTaxa("A")]).DeveFalharCom(erro);
    }

    [Fact]
    public void Deve_rejeitar_quando_identificacao_aliquota_ou_coberturas_invalidas()
    {
        CoberturaProduto[] coberturas = [CoberturaComTaxa("A")];

        Produto.Criar("AUTO", " ", "Descrição", 0.0738m, coberturas).DeveFalharCom("Produto.IdentificacaoInvalida");
        Produto.Criar("AUTO", "Nome", null!, 0.0738m, coberturas).DeveFalharCom("Produto.IdentificacaoInvalida");
        Produto.Criar("AUTO", "Nome", "Descrição", 1m, coberturas).DeveFalharCom("Produto.AliquotaIofInvalida");
        Produto.Criar("AUTO", "Nome", "Descrição", -0.01m, coberturas).DeveFalharCom("Produto.AliquotaIofInvalida");
        Produto.Criar("AUTO", "Nome", "Descrição", 0.0738m, []).DeveFalharCom("Produto.SemCoberturas");
        Produto.Criar("AUTO", "Nome", "Descrição", 0.0738m, null!).DeveFalharCom("Produto.SemCoberturas");
        Produto.Criar("AUTO", "Nome", "Descrição", 0.0738m, [CoberturaComTaxa("A"), CoberturaComTaxa("A")])
            .DeveFalharCom("Produto.CoberturaDuplicada");
    }

    private static CoberturaProduto CoberturaComTaxa(string codigo) =>
        CoberturaProduto.ComTaxa(codigo, "Nome", "Descrição", true, TipoCalculo.TaxaSobreLmi, 0.01m, 1_000m, 10_000m).Value;
}

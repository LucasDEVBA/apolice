using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Tarifacao;

// Validações comuns da seção 4.2 e o despacho da Strategy pelo código do produto.
public sealed class TarifadorTests
{
    private static readonly Tarifador Tarifador = Tarifador.Padrao();

    [Fact]
    public void Deve_rejeitar_quando_dados_de_risco_de_outro_produto()
    {
        Tarifador.Calcular(CatalogoPadrao.Auto(), Cenarios.SeguradoPadrao(), new DadosRiscoVida(false), Cenarios.CoberturasAutoBasicas(), Cenarios.Hoje)
            .DeveFalharCom("Tarifacao.ProdutoIncompativel", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_quando_estrategia_recebe_produto_de_outro_tipo()
    {
        new TarifacaoAuto().Calcular(CatalogoPadrao.Vida(), Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), [], Cenarios.Hoje)
            .DeveFalharCom("Tarifacao.ProdutoIncompativel");
    }

    [Fact]
    public void Deve_rejeitar_quando_falta_cobertura_obrigatoria()
    {
        CalcularAuto([new("CASCO", null)]).DeveFalharCom("Tarifacao.CoberturaObrigatoriaAusente");
    }

    [Fact]
    public void Deve_rejeitar_quando_codigo_de_cobertura_inexistente()
    {
        CalcularAuto([.. Cenarios.CoberturasAutoBasicas(), new("XYZ", null)]).DeveFalharCom("Tarifacao.CoberturaInexistente");
        CalcularAuto([.. Cenarios.CoberturasAutoBasicas(), null!]).DeveFalharCom("Tarifacao.CoberturaInexistente");
    }

    [Fact]
    public void Deve_rejeitar_quando_cobertura_duplicada()
    {
        CalcularAuto([.. Cenarios.CoberturasAutoBasicas(), new("RCF_DM", 200_000m)]).DeveFalharCom("Tarifacao.CoberturaDuplicada");
    }

    [Fact]
    public void Deve_ordenar_itens_pelo_catalogo_quando_pedido_vem_em_outra_ordem()
    {
        var resultado = CalcularAuto([new("ASSISTENCIA_24H", null), new("RCF_DM", 100_000m), new("CASCO", null)]).DeveTerSucesso();

        resultado.Itens.Select(i => i.Codigo).ShouldBe(["CASCO", "RCF_DM", "ASSISTENCIA_24H"]);
    }

    [Fact]
    public void Deve_retornar_erro_quando_nao_ha_estrategia_para_o_produto()
    {
        var soAuto = new Tarifador([new TarifacaoAuto()]);

        soAuto.Calcular(CatalogoPadrao.Vida(), Cenarios.SeguradoPadrao(), new DadosRiscoVida(false), [new("MORTE", 100_000m)], Cenarios.Hoje)
            .DeveFalharCom("Tarifacao.ProdutoNaoSuportado");
    }

    [Fact]
    public void Deve_lancar_quando_duas_estrategias_para_o_mesmo_produto()
    {
        Should.Throw<ArgumentException>(() => new Tarifador([new TarifacaoAuto(), new TarifacaoAuto()]));
    }

    [Fact]
    public void Deve_lancar_quando_argumentos_nulos()
    {
        Should.Throw<ArgumentNullException>(() => new Tarifador(null!));
        Should.Throw<ArgumentNullException>(() => Tarifador.Calcular(null!, Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), [], Cenarios.Hoje));
        Should.Throw<ArgumentNullException>(() => CalcularAuto(null!));
    }

    [Theory]
    [InlineData("0.01", 1)]
    [InlineData("60.00", 1)]
    [InlineData("119.99", 1)] // floor(1,99) = 1
    [InlineData("120.00", 2)]
    [InlineData("599.99", 9)]
    [InlineData("600.00", 10)]
    [InlineData("100000.00", 10)] // teto de 10 parcelas
    public void Deve_limitar_parcelas_quando_parcela_ficaria_abaixo_de_60(string premioTotal, int maxParcelas)
    {
        ResultadoTarifacao.CalcularMaxParcelas(Valor.De(premioTotal)).ShouldBe(maxParcelas);
    }

    [Fact]
    public void Deve_somar_itens_e_arredondar_iof_quando_monta_resultado()
    {
        var resultado = ResultadoTarifacao.Criar([Item("A", 100.00m), Item("B", 0.10m)], [], 0.10m);

        resultado.PremioLiquido.ShouldBe(100.10m);
        resultado.Iof.ShouldBe(10.01m); // 100,10 × 0,10 = 10,01
        resultado.PremioTotal.ShouldBe(110.11m);
        resultado.MaxParcelas.ShouldBe(1); // floor(110,11 / 60) = 1
    }

    [Fact]
    public void Deve_manter_itens_e_fatores_intactos_quando_quem_montou_altera_as_listas()
    {
        var itens = new List<ItemTarifado> { Item("A", 100.00m) };
        var fatores = new List<FatorAplicado> { new("Fator", 1.10m) };
        var resultado = ResultadoTarifacao.Criar(itens, fatores, 0.10m);

        itens.Add(Item("B", 1_000_000.00m));
        fatores.Clear();

        resultado.Itens.ShouldHaveSingleItem().Codigo.ShouldBe("A");
        resultado.Fatores.ShouldHaveSingleItem();
        resultado.PremioLiquido.ShouldBe(resultado.Itens.Sum(i => i.PremioLiquido));
        (resultado.Itens is List<ItemTarifado>).ShouldBeFalse();
        (CalcularAuto(Cenarios.CoberturasAutoBasicas()).Value.Itens is List<ItemTarifado>).ShouldBeFalse();
    }

    [Fact]
    public void Deve_ser_igual_quando_tarifacoes_do_mesmo_pedido()
    {
        var a = CalcularAuto(Cenarios.CoberturasAutoComAssistencia()).Value;
        var b = CalcularAuto(Cenarios.CoberturasAutoComAssistencia()).Value;
        var outra = CalcularAuto(Cenarios.CoberturasAutoBasicas()).Value;

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(outra);
        a.Equals(null).ShouldBeFalse();
        a.ShouldNotBe(ResultadoTarifacao.Criar(a.Itens, a.Fatores, 0.0038m)); // mesma lista, outra alíquota
    }

    [Fact]
    public void Deve_considerar_os_fatores_quando_compara_resultados()
    {
        var comBonus = ResultadoTarifacao.Criar([Item("A", 100.00m)], [new("Bônus classe 5", 0.85m)], 0.10m);
        var mesmoBonus = ResultadoTarifacao.Criar([Item("A", 100.00m)], [new("Bônus classe 5", 0.85m)], 0.10m);
        var outroBonus = ResultadoTarifacao.Criar([Item("A", 100.00m)], [new("Bônus classe 6", 0.82m)], 0.10m);

        comBonus.ShouldBe(mesmoBonus);
        comBonus.GetHashCode().ShouldBe(mesmoBonus.GetHashCode());
        comBonus.ShouldNotBe(outroBonus);
    }

    [Fact]
    public void Deve_comparar_fatores_tarifarios_pelo_conteudo_dos_alvos()
    {
        var alvos = new[] { "ROUBO" };
        var a = new FatorTarifario("Casa (roubo)", 1.30m, alvos);
        var b = new FatorTarifario("Casa (roubo)", 1.30m, new List<string> { "ROUBO" });

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(new FatorTarifario("Casa (roubo)", 1.30m));
        new FatorTarifario("Apartamento", 0.80m).ShouldBe(new FatorTarifario("Apartamento", 0.80m));
        new FatorTarifario("Apartamento", 0.80m).Equals(null).ShouldBeFalse();

        alvos[0] = "INCENDIO"; // cópia defensiva: alterar o array de origem não muda o fator
        (a.AplicaA("ROUBO"), a.AplicaA("INCENDIO")).ShouldBe((true, false));
    }

    [Fact]
    public void Deve_lancar_quando_cobertura_persistida_de_premio_fixo_sem_premio()
    {
        // Simula a linha corrompida que o EF materializaria sem passar pelas fábricas.
        var produto = CatalogoPadrao.Auto();
        typeof(CoberturaProduto).GetProperty(nameof(CoberturaProduto.PremioFixo))!.SetValue(produto.ObterCobertura("VIDROS"), null);

        Should.Throw<DadoPersistidoInvalidoException>(() =>
            Tarifador.Calcular(produto, Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), [.. Cenarios.CoberturasAutoBasicas(), new("VIDROS", null)], Cenarios.Hoje));
    }

    [Fact]
    public void Deve_lancar_quando_cobertura_persistida_com_tipo_de_calculo_desconhecido()
    {
        var produto = CatalogoPadrao.Auto();
        typeof(CoberturaProduto).GetProperty(nameof(CoberturaProduto.TipoCalculo))!.SetValue(produto.ObterCobertura("RCF_DM"), (TipoCalculo)99);

        Should.Throw<DadoPersistidoInvalidoException>(() =>
            Tarifador.Calcular(produto, Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), Cenarios.CoberturasAutoBasicas(), Cenarios.Hoje));
    }

    private static ItemTarifado Item(string codigo, decimal premio) => new(codigo, codigo, 1m, premio, true, true);

    private static Result<ResultadoTarifacao> CalcularAuto(IReadOnlyList<CoberturaSolicitada> coberturas) =>
        Tarifador.Calcular(CatalogoPadrao.Auto(), Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), coberturas, Cenarios.Hoje);
}

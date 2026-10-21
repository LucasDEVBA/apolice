using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Produtos;

public sealed class CoberturaProdutoTests
{
    [Fact]
    public void Deve_criar_cobertura_por_taxa_quando_dados_validos()
    {
        var cobertura = CoberturaProduto.ComTaxa("X", "Nome", "Descrição", false, TipoCalculo.TaxaPorMil, 0.5m, 0m, 10m).DeveTerSucesso();

        cobertura.CalculaPorTaxa.ShouldBeTrue();
        cobertura.PremioFixo.ShouldBeNull();
        cobertura.LmiFixo.ShouldBeNull();
        cobertura.PermiteSinistro.ShouldBeTrue();
    }

    [Fact]
    public void Deve_usar_faixa_degenerada_quando_premio_fixo()
    {
        var cobertura = CoberturaProduto.ComPremioFixo("X", "Nome", "Descrição", false, 50m, 2_000m, false).DeveTerSucesso();

        cobertura.CalculaPorTaxa.ShouldBeFalse();
        cobertura.TipoCalculo.ShouldBe(TipoCalculo.PremioFixo);
        cobertura.PremioFixo.ShouldBe(50m);
        (cobertura.LmiMinimo, cobertura.LmiMaximo, cobertura.LmiFixo).ShouldBe((2_000m, 2_000m, (decimal?)2_000m));
        cobertura.PermiteSinistro.ShouldBeFalse();
    }

    [Fact]
    public void Deve_rejeitar_quando_identificacao_invalida()
    {
        Taxa(codigo: " ").DeveFalharCom("Produto.CoberturaCodigoInvalido");
        Taxa(codigo: new string('A', 31)).DeveFalharCom("Produto.CoberturaCodigoInvalido");
        Taxa(nome: "").DeveFalharCom("Produto.CoberturaNomeInvalido");
        Taxa(nome: new string('a', 121)).DeveFalharCom("Produto.CoberturaNomeInvalido");
        Taxa(descricao: null!).DeveFalharCom("Produto.CoberturaDescricaoInvalida");
        Taxa(descricao: new string('a', 501)).DeveFalharCom("Produto.CoberturaDescricaoInvalida");
        CoberturaProduto.ComPremioFixo(" ", "Nome", "Descrição", false, 1m, 0m, true).DeveFalharCom("Produto.CoberturaCodigoInvalido");
    }

    [Fact]
    public void Deve_rejeitar_quando_parametros_de_calculo_invalidos()
    {
        Taxa(tipo: TipoCalculo.PremioFixo).DeveFalharCom("Produto.TipoCalculoInvalido");
        Taxa(taxa: 0m).DeveFalharCom("Produto.TaxaInvalida");
        Taxa(lmiMinimo: -1m).DeveFalharCom("Produto.LmiInvalido");
        Taxa(lmiMinimo: 10m, lmiMaximo: 9m).DeveFalharCom("Produto.LmiInvalido");
        CoberturaProduto.ComPremioFixo("X", "Nome", "Descrição", false, 0m, 0m, true).DeveFalharCom("Produto.PremioFixoInvalido");
        CoberturaProduto.ComPremioFixo("X", "Nome", "Descrição", false, 1m, -1m, true).DeveFalharCom("Produto.LmiInvalido");
    }

    private static Result<CoberturaProduto> Taxa(
        string codigo = "X",
        string nome = "Nome",
        string descricao = "Descrição",
        TipoCalculo tipo = TipoCalculo.TaxaSobreLmi,
        decimal taxa = 0.01m,
        decimal lmiMinimo = 0m,
        decimal lmiMaximo = 10m) =>
        CoberturaProduto.ComTaxa(codigo, nome, descricao, true, tipo, taxa, lmiMinimo, lmiMaximo);
}

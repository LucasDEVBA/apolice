using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tests.Produtos;

// Trava o catálogo na tabela da seção 4.2 do SPEC: o seeder e a tarifação dependem destes números.
public sealed class CatalogoPadraoTests
{
    [Fact]
    public void Deve_ter_auto_residencial_e_vida_com_as_aliquotas_de_iof_quando_lista_produtos()
    {
        var produtos = CatalogoPadrao.Produtos();

        produtos.Select(p => (p.Codigo, p.AliquotaIof)).ShouldBe([("AUTO", 0.0738m), ("RESID", 0.0738m), ("VIDA", 0.0038m)]);
        produtos.ShouldAllBe(p => !string.IsNullOrWhiteSpace(p.Nome) && !string.IsNullOrWhiteSpace(p.Descricao));
    }

    [Fact]
    public void Deve_ter_as_coberturas_da_tabela_quando_produto_auto()
    {
        Projetar(CatalogoPadrao.Auto()).ShouldBe(
        [
            new("CASCO", "Colisão, incêndio e roubo", true, TipoCalculo.TaxaSobreLmi, 0.0320m, null, 10_000m, 500_000m, null, true),
            new("RCF_DM", "RC danos materiais a terceiros", true, TipoCalculo.TaxaSobreLmi, 0.0060m, null, 50_000m, 500_000m, null, true),
            new("RCF_DC", "RC danos corporais a terceiros", false, TipoCalculo.TaxaSobreLmi, 0.0040m, null, 50_000m, 500_000m, null, true),
            new("VIDROS", "Vidros, faróis e retrovisores", false, TipoCalculo.PremioFixo, 0m, 220.00m, 3_000m, 3_000m, 3_000m, true),
            new("CARRO_RESERVA", "Carro reserva (15 dias)", false, TipoCalculo.PremioFixo, 0m, 180.00m, 0m, 0m, 0m, false),
            new("ASSISTENCIA_24H", "Assistência 24h", false, TipoCalculo.PremioFixo, 0m, 120.00m, 0m, 0m, 0m, false),
        ]);
    }

    [Fact]
    public void Deve_ter_as_coberturas_da_tabela_quando_produto_residencial()
    {
        Projetar(CatalogoPadrao.Residencial()).ShouldBe(
        [
            new("INCENDIO", "Incêndio, raio e explosão", true, TipoCalculo.TaxaSobreLmi, 0.00045m, null, 10_000m, 5_000_000m, null, true),
            new("DANOS_ELETRICOS", "Danos elétricos", false, TipoCalculo.TaxaSobreLmi, 0.0120m, null, 5_000m, 50_000m, null, true),
            new("ROUBO", "Roubo e furto qualificado", false, TipoCalculo.TaxaSobreLmi, 0.0180m, null, 5_000m, 100_000m, null, true),
            new("RC_FAMILIAR", "Responsabilidade civil familiar", false, TipoCalculo.TaxaSobreLmi, 0.0035m, null, 10_000m, 200_000m, null, true),
            new("VENDAVAL", "Vendaval e granizo", false, TipoCalculo.TaxaSobreLmi, 0.0060m, null, 5_000m, 200_000m, null, true),
        ]);
    }

    [Fact]
    public void Deve_ter_as_coberturas_da_tabela_quando_produto_vida()
    {
        Projetar(CatalogoPadrao.Vida()).ShouldBe(
        [
            new("MORTE", "Morte natural ou acidental", true, TipoCalculo.TaxaPorMil, 0.80m, null, 20_000m, 2_000_000m, null, true),
            new("IPA", "Invalidez permanente por acidente", false, TipoCalculo.TaxaPorMil, 0.35m, null, 10_000m, 2_000_000m, null, true),
            new("FUNERAL", "Assistência funeral", false, TipoCalculo.PremioFixo, 0m, 90.00m, 7_000m, 7_000m, 7_000m, true),
        ]);
    }

    [Fact]
    public void Deve_devolver_instancias_novas_quando_chamado_de_novo()
    {
        // O seeder e os testes alteram taxas: compartilhar a instância vazaria estado entre eles.
        CatalogoPadrao.Auto().ShouldNotBeSameAs(CatalogoPadrao.Auto());
    }

    private static List<CoberturaEsperada> Projetar(Produto produto) =>
    [
        .. produto.Coberturas.Select(c => new CoberturaEsperada(
            c.Codigo, c.Nome, c.Obrigatoria, c.TipoCalculo, c.Taxa, c.PremioFixo, c.LmiMinimo, c.LmiMaximo, c.LmiFixo, c.PermiteSinistro)),
    ];

    private sealed record CoberturaEsperada(
        string Codigo,
        string Nome,
        bool Obrigatoria,
        TipoCalculo TipoCalculo,
        decimal Taxa,
        decimal? PremioFixo,
        decimal LmiMinimo,
        decimal LmiMaximo,
        decimal? LmiFixo,
        bool PermiteSinistro);
}

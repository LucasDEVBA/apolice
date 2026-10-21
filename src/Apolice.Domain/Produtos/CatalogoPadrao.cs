namespace Apolice.Domain.Produtos;

// Catálogo da seção 4.2 do SPEC: fonte única para o seeder e para os testes de tarifação.
// Os valores são fixos em código, então uma falha de construção aqui é bug (Value lança).
public static class CatalogoPadrao
{
    private const decimal AliquotaIofSeguroDano = 0.0738m;
    private const decimal AliquotaIofSeguroPessoa = 0.0038m;

    public static IReadOnlyList<Produto> Produtos() => [Auto(), Residencial(), Vida()];

    public static Produto Auto() => Produto.Criar(
        CodigosProduto.Auto,
        "Seguro Auto",
        "Proteção para o veículo e responsabilidade civil perante terceiros.",
        AliquotaIofSeguroDano,
        [
            ComTaxa(CodigosCobertura.Casco, "Colisão, incêndio e roubo", "Danos ao veículo por colisão, incêndio, roubo ou furto, até o valor do veículo.", true, 0.0320m, 10_000m, 500_000m),
            ComTaxa(CodigosCobertura.RcfDanosMateriais, "RC danos materiais a terceiros", "Danos materiais causados a veículos e bens de terceiros.", true, 0.0060m, 50_000m, 500_000m),
            ComTaxa(CodigosCobertura.RcfDanosCorporais, "RC danos corporais a terceiros", "Danos corporais causados a terceiros envolvidos no acidente.", false, 0.0040m, 50_000m, 500_000m),
            ComPremioFixo(CodigosCobertura.Vidros, "Vidros, faróis e retrovisores", "Reparo ou troca de vidros, faróis, lanternas e retrovisores.", 220.00m, 3_000m, true),
            ComPremioFixo(CodigosCobertura.CarroReserva, "Carro reserva (15 dias)", "Veículo reserva por até 15 dias após sinistro coberto.", 180.00m, 0m, false),
            ComPremioFixo(CodigosCobertura.Assistencia24h, "Assistência 24h", "Guincho, chaveiro, troca de pneu e pane seca, 24 horas por dia.", 120.00m, 0m, false),
        ]).Value;

    public static Produto Residencial() => Produto.Criar(
        CodigosProduto.Residencial,
        "Seguro Residencial",
        "Proteção para o imóvel e seu conteúdo contra os principais riscos residenciais.",
        AliquotaIofSeguroDano,
        [
            ComTaxa(CodigosCobertura.Incendio, "Incêndio, raio e explosão", "Danos ao imóvel causados por incêndio, queda de raio ou explosão.", true, 0.00045m, 10_000m, 5_000_000m),
            ComTaxa(CodigosCobertura.DanosEletricos, "Danos elétricos", "Danos a instalações e aparelhos por variação de tensão ou curto-circuito.", false, 0.0120m, 5_000m, 50_000m),
            ComTaxa(CodigosCobertura.Roubo, "Roubo e furto qualificado", "Subtração de bens do imóvel mediante arrombamento ou violência.", false, 0.0180m, 5_000m, 100_000m),
            ComTaxa(CodigosCobertura.RcFamiliar, "Responsabilidade civil familiar", "Danos involuntários causados a terceiros pelo segurado e sua família.", false, 0.0035m, 10_000m, 200_000m),
            ComTaxa(CodigosCobertura.Vendaval, "Vendaval e granizo", "Danos causados por vendaval, furacão, ciclone, tornado e granizo.", false, 0.0060m, 5_000m, 200_000m),
        ]).Value;

    public static Produto Vida() => Produto.Criar(
        CodigosProduto.Vida,
        "Seguro de Vida",
        "Proteção financeira para a família em caso de morte ou invalidez.",
        AliquotaIofSeguroPessoa,
        [
            ComTaxaPorMil(CodigosCobertura.Morte, "Morte natural ou acidental", "Capital pago aos beneficiários em caso de morte por qualquer causa.", true, 0.80m, 20_000m, 2_000_000m),
            ComTaxaPorMil(CodigosCobertura.InvalidezPermanenteAcidente, "Invalidez permanente por acidente", "Capital pago em caso de invalidez permanente, total ou parcial, por acidente.", false, 0.35m, 10_000m, 2_000_000m),
            ComPremioFixo(CodigosCobertura.Funeral, "Assistência funeral", "Organização e custeio do funeral até o limite contratado.", 90.00m, 7_000m, true),
        ]).Value;

    private static CoberturaProduto ComTaxa(string codigo, string nome, string descricao, bool obrigatoria, decimal taxa, decimal lmiMinimo, decimal lmiMaximo) =>
        CoberturaProduto.ComTaxa(codigo, nome, descricao, obrigatoria, TipoCalculo.TaxaSobreLmi, taxa, lmiMinimo, lmiMaximo).Value;

    private static CoberturaProduto ComTaxaPorMil(string codigo, string nome, string descricao, bool obrigatoria, decimal taxa, decimal lmiMinimo, decimal lmiMaximo) =>
        CoberturaProduto.ComTaxa(codigo, nome, descricao, obrigatoria, TipoCalculo.TaxaPorMil, taxa, lmiMinimo, lmiMaximo).Value;

    private static CoberturaProduto ComPremioFixo(string codigo, string nome, string descricao, decimal premio, decimal lmiFixo, bool permiteSinistro) =>
        CoberturaProduto.ComPremioFixo(codigo, nome, descricao, false, premio, lmiFixo, permiteSinistro).Value;
}

using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Tarifacao;

// Cada cobertura com faixa de LMI do SPEC 4.2 nas quatro bordas: mínimo, mínimo − 0,01, máximo e máximo + 0,01.
public sealed class BordasDeLmiTests
{
    private static readonly Tarifador Tarifador = Tarifador.Padrao();

    public static TheoryData<string, string, bool> Bordas => new()
    {
        { "DANOS_ELETRICOS", "5000", true }, { "DANOS_ELETRICOS", "4999.99", false },
        { "DANOS_ELETRICOS", "50000", true }, { "DANOS_ELETRICOS", "50000.01", false },
        { "ROUBO", "5000", true }, { "ROUBO", "4999.99", false }, { "ROUBO", "100000", true }, { "ROUBO", "100000.01", false },
        { "RC_FAMILIAR", "10000", true }, { "RC_FAMILIAR", "9999.99", false },
        { "RC_FAMILIAR", "200000", true }, { "RC_FAMILIAR", "200000.01", false },
        { "VENDAVAL", "5000", true }, { "VENDAVAL", "4999.99", false }, { "VENDAVAL", "200000", true }, { "VENDAVAL", "200000.01", false },
        { "INCENDIO", "5000000", true }, { "INCENDIO", "5000000.01", false },
        { "RCF_DC", "50000", true }, { "RCF_DC", "49999.99", false }, { "RCF_DC", "500000", true }, { "RCF_DC", "500000.01", false },
        { "MORTE", "20000", true }, { "MORTE", "2000000.01", false },
        { "IPA", "10000", true }, { "IPA", "9999.99", false },
    };

    [Theory]
    [MemberData(nameof(Bordas))]
    public void Deve_aceitar_so_dentro_da_faixa_quando_lmi_na_borda(string cobertura, string lmi, bool valido)
    {
        var resultado = Calcular(cobertura, Valor.De(lmi));

        if (valido)
        {
            resultado.DeveTerSucesso().Itens.Single(i => i.Codigo == cobertura).Lmi.ShouldBe(Valor.De(lmi));
            return;
        }

        resultado.DeveFalharCom("Tarifacao.LmiForaDoLimite", ErrorType.Validacao);
    }

    private static Result<ResultadoTarifacao> Calcular(string cobertura, decimal lmi) => cobertura switch
    {
        "RCF_DC" => Tarifador.Calcular(
            CatalogoPadrao.Auto(), Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), [.. Cenarios.CoberturasAutoBasicas(), new(cobertura, lmi)], Cenarios.Hoje),
        "MORTE" => CalcularVida([new(cobertura, lmi)]),
        "IPA" => CalcularVida([new("MORTE", 2_000_000m), new(cobertura, lmi)]),
        "INCENDIO" => CalcularResidencial(5_000_000m, [new(cobertura, lmi)]),
        _ => CalcularResidencial(100_000m, [new("INCENDIO", 10_000m), new(cobertura, lmi)]),
    };

    private static Result<ResultadoTarifacao> CalcularVida(IReadOnlyList<CoberturaSolicitada> coberturas) =>
        Tarifador.Calcular(CatalogoPadrao.Vida(), Cenarios.SeguradoPadrao(), new DadosRiscoVida(false), coberturas, Cenarios.Hoje);

    private static Result<ResultadoTarifacao> CalcularResidencial(decimal valorImovel, IReadOnlyList<CoberturaSolicitada> coberturas) =>
        Tarifador.Calcular(
            CatalogoPadrao.Residencial(),
            Cenarios.SeguradoPadrao(),
            new DadosRiscoResidencial(valorImovel, TipoImovel.Casa, "40000-000", Habitual: true),
            coberturas,
            Cenarios.Hoje);
}

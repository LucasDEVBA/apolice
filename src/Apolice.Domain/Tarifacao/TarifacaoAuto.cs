using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

public sealed class TarifacaoAuto : TarifacaoBase<DadosRiscoAuto>
{
    private const decimal ValorVeiculoMinimo = 10_000m;
    private const decimal ValorVeiculoMaximo = 500_000m;
    private const int AnosFabricacaoRetroativos = 20;
    private const int AnosFabricacaoFuturos = 1;
    private const int IdadeCondutorMinima = 18;
    private const int IdadeCondutorMaxima = 90;
    private const int ClasseBonusMinima = 0;
    private const int ClasseBonusMaxima = 10;
    private const decimal DescontoPorClasseBonus = 0.03m;
    private const int IdadeVeiculoSemAgravo = 10;
    private const decimal FatorVeiculoAntigo = 1.10m;
    private const decimal FatorUsoComercial = 1.35m;

    private static readonly FaixaFator[] FaixasIdadeCondutor =
    [
        new(IdadeCondutorMinima, 25, 1.40m),
        new(26, 35, 1.10m),
        new(36, 60, 1.00m),
        new(61, IdadeCondutorMaxima, 1.15m),
    ];

    // Região pelo 1º dígito do CEP de pernoite; dígitos ausentes do mapa são neutros (1,00).
    private static readonly Dictionary<char, (string Regiao, decimal Fator)> FatoresRegiao = new()
    {
        ['0'] = ("Grande São Paulo", 1.25m),
        ['2'] = ("RJ/ES", 1.30m),
        ['4'] = ("BA/SE", 1.10m),
    };

    public override string ProdutoCodigo => CodigosProduto.Auto;

    protected override Result ValidarDados(DadosRiscoAuto dados, Segurado segurado, DateOnly dataReferencia)
    {
        var veiculo = ValidarVeiculo(dados, dataReferencia);
        if (veiculo.IsFailure)
        {
            return veiculo;
        }

        if (!CepValido(dados.CepPernoite))
        {
            return TarifacaoErros.CepInvalido;
        }

        if (dados.IdadeCondutor is < IdadeCondutorMinima or > IdadeCondutorMaxima)
        {
            return TarifacaoErros.IdadeCondutorForaDoLimite(IdadeCondutorMinima, IdadeCondutorMaxima);
        }

        return dados.ClasseBonus is < ClasseBonusMinima or > ClasseBonusMaxima
            ? TarifacaoErros.ClasseBonusInvalida(ClasseBonusMinima, ClasseBonusMaxima)
            : Result.Success();
    }

    protected override decimal? LmiDefinidoPeloRisco(CoberturaProduto cobertura, DadosRiscoAuto dados) =>
        cobertura.Codigo == CodigosCobertura.Casco ? dados.ValorVeiculo : null;

    protected override IReadOnlyList<FatorTarifario> DefinirFatores(DadosRiscoAuto dados, Segurado segurado, DateOnly dataReferencia)
    {
        // ValidarDados roda antes e já limitou a idade a 18–90, que as faixas cobrem por inteiro.
        var faixa = Array.Find(FaixasIdadeCondutor, f => f.Contem(dados.IdadeCondutor))!;
        var fatores = new List<FatorTarifario>
        {
            new($"Idade do condutor: {faixa.Minimo} a {faixa.Maximo} anos", faixa.Fator),
            FatorRegiao(dados.CepPernoite),
            new("Uso comercial", dados.UsoComercial ? FatorUsoComercial : FatorNeutro),
        };

        if (dataReferencia.Year - dados.AnoFabricacao > IdadeVeiculoSemAgravo)
        {
            fatores.Add(new FatorTarifario($"Veículo com mais de {IdadeVeiculoSemAgravo} anos", FatorVeiculoAntigo));
        }

        fatores.Add(new FatorTarifario($"Bônus classe {dados.ClasseBonus}", FatorNeutro - (DescontoPorClasseBonus * dados.ClasseBonus)));
        return fatores;
    }

    // O valor do veículo é o LMI do CASCO: precisa estar em centavos exatos, como todo LMI.
    private static Result ValidarVeiculo(DadosRiscoAuto dados, DateOnly dataReferencia)
    {
        if (dados.ValorVeiculo is < ValorVeiculoMinimo or > ValorVeiculoMaximo)
        {
            return TarifacaoErros.ValorVeiculoForaDoLimite(ValorVeiculoMinimo, ValorVeiculoMaximo);
        }

        if (!Dinheiro.EstaEmCentavos(dados.ValorVeiculo))
        {
            return TarifacaoErros.ValorMonetarioInvalido("valor do veículo");
        }

        var anoMinimo = dataReferencia.Year - AnosFabricacaoRetroativos;
        var anoMaximo = dataReferencia.Year + AnosFabricacaoFuturos;
        return dados.AnoFabricacao < anoMinimo || dados.AnoFabricacao > anoMaximo
            ? TarifacaoErros.AnoFabricacaoInvalido(anoMinimo, anoMaximo)
            : Result.Success();
    }

    private static FatorTarifario FatorRegiao(string cep) =>
        FatoresRegiao.TryGetValue(cep[0], out var regiao)
            ? new FatorTarifario($"Região de pernoite: {regiao.Regiao}", regiao.Fator)
            : new FatorTarifario("Região de pernoite: demais regiões", FatorNeutro);
}

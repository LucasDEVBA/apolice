using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

public sealed class TarifacaoVida : TarifacaoBase<DadosRiscoVida>
{
    private const int IdadeMinima = 18;
    private const int IdadeMaxima = 70;
    private const decimal FatorFumante = 1.50m;

    private static readonly FaixaFator[] FaixasEtarias =
    [
        new(IdadeMinima, 30, 1.00m),
        new(31, 40, 1.50m),
        new(41, 50, 3.00m),
        new(51, 60, 6.25m),
        new(61, IdadeMaxima, 12.50m),
    ];

    private static readonly string[] SomenteMorte = [CodigosCobertura.Morte];

    public override string ProdutoCodigo => CodigosProduto.Vida;

    protected override Result ValidarDados(DadosRiscoVida dados, Segurado segurado, DateOnly dataReferencia) =>
        segurado.IdadeEm(dataReferencia) is < IdadeMinima or > IdadeMaxima
            ? TarifacaoErros.IdadeSeguradoForaDoLimite(IdadeMinima, IdadeMaxima)
            : Result.Success();

    // Produto.Criar aceita um VIDA sem MORTE (o catálogo é que a torna obrigatória): sem ela, não há teto para a IPA.
    protected override Result ValidarLmis(IReadOnlyDictionary<string, decimal> lmis, DadosRiscoVida dados) =>
        lmis.TryGetValue(CodigosCobertura.InvalidezPermanenteAcidente, out var lmiIpa)
        && lmis.TryGetValue(CodigosCobertura.Morte, out var lmiMorte)
        && lmiIpa > lmiMorte
            ? TarifacaoErros.LmiIpaAcimaDaMorte
            : Result.Success();

    protected override IReadOnlyList<FatorTarifario> DefinirFatores(DadosRiscoVida dados, Segurado segurado, DateOnly dataReferencia)
    {
        // ValidarDados roda antes e já limitou a idade a 18–70, que as faixas cobrem por inteiro.
        var faixa = Array.Find(FaixasEtarias, f => f.Contem(segurado.IdadeEm(dataReferencia)))!;
        var fatores = new List<FatorTarifario>
        {
            new($"Faixa etária: {faixa.Minimo} a {faixa.Maximo} anos (morte)", faixa.Fator, SomenteMorte),
        };

        if (dados.Fumante)
        {
            fatores.Add(new FatorTarifario("Fumante (morte)", FatorFumante, SomenteMorte));
        }

        return fatores;
    }
}

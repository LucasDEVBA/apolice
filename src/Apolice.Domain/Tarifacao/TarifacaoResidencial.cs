using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

public sealed class TarifacaoResidencial : TarifacaoBase<DadosRiscoResidencial>
{
    private const decimal ValorImovelMinimo = 50_000m;
    private const decimal ValorImovelMaximo = 5_000_000m;
    private const decimal FatorApartamento = 0.80m;
    private const decimal FatorCasaRoubo = 1.30m;
    private const decimal FatorNaoHabitual = 1.30m;

    private static readonly string[] SomenteRoubo = [CodigosCobertura.Roubo];

    public override string ProdutoCodigo => CodigosProduto.Residencial;

    protected override Result ValidarDados(DadosRiscoResidencial dados, Segurado segurado, DateOnly dataReferencia)
    {
        if (dados.ValorImovel is < ValorImovelMinimo or > ValorImovelMaximo)
        {
            return TarifacaoErros.ValorImovelForaDoLimite(ValorImovelMinimo, ValorImovelMaximo);
        }

        // O valor do imóvel é o teto do LMI de incêndio: centavos exatos pelo mesmo motivo dos LMIs.
        if (!Dinheiro.EstaEmCentavos(dados.ValorImovel))
        {
            return TarifacaoErros.ValorMonetarioInvalido("valor do imóvel");
        }

        if (!CepValido(dados.Cep))
        {
            return TarifacaoErros.CepInvalido;
        }

        return Enum.IsDefined(dados.TipoImovel) ? Result.Success() : TarifacaoErros.TipoImovelInvalido;
    }

    protected override Result ValidarLmis(IReadOnlyDictionary<string, decimal> lmis, DadosRiscoResidencial dados) =>
        lmis.TryGetValue(CodigosCobertura.Incendio, out var lmiIncendio) && lmiIncendio > dados.ValorImovel
            ? TarifacaoErros.LmiAcimaDoValorImovel
            : Result.Success();

    protected override IReadOnlyList<FatorTarifario> DefinirFatores(DadosRiscoResidencial dados, Segurado segurado, DateOnly dataReferencia)
    {
        var fatores = new List<FatorTarifario>
        {
            dados.TipoImovel == TipoImovel.Apartamento
                ? new FatorTarifario("Apartamento", FatorApartamento)
                : new FatorTarifario("Casa (roubo)", FatorCasaRoubo, SomenteRoubo),
        };

        if (!dados.Habitual)
        {
            fatores.Add(new FatorTarifario("Imóvel não habitual (veraneio)", FatorNaoHabitual));
        }

        return fatores;
    }
}

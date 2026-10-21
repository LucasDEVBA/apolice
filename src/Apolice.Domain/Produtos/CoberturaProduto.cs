using Apolice.Domain.Comum;

namespace Apolice.Domain.Produtos;

// Entidade filha do agregado Produto: a identidade (Codigo) só é única dentro do produto.
public sealed class CoberturaProduto
{
    private const decimal TaxaMaxima = 1m;
    private const int CodigoTamanhoMaximo = 30;
    private const int NomeTamanhoMaximo = 120;
    private const int DescricaoTamanhoMaximo = 500;

    private CoberturaProduto()
    {
        Codigo = null!;
        Nome = null!;
        Descricao = null!;
    }

    private CoberturaProduto(string codigo, string nome, string descricao, bool obrigatoria, TipoCalculo tipoCalculo)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        Obrigatoria = obrigatoria;
        TipoCalculo = tipoCalculo;
    }

    public string Codigo { get; private set; }

    public string Nome { get; private set; }

    public string Descricao { get; private set; }

    public bool Obrigatoria { get; private set; }

    public TipoCalculo TipoCalculo { get; private set; }

    public decimal Taxa { get; private set; }

    public decimal? PremioFixo { get; private set; }

    public decimal LmiMinimo { get; private set; }

    public decimal LmiMaximo { get; private set; }

    public decimal? LmiFixo { get; private set; }

    public bool PermiteSinistro { get; private set; }

    public bool CalculaPorTaxa => TipoCalculo != TipoCalculo.PremioFixo;

    public static bool TaxaValida(decimal taxa) => taxa > 0 && taxa <= TaxaMaxima;

    public static Result<CoberturaProduto> ComTaxa(
        string codigo,
        string nome,
        string descricao,
        bool obrigatoria,
        TipoCalculo tipoCalculo,
        decimal taxa,
        decimal lmiMinimo,
        decimal lmiMaximo,
        bool permiteSinistro = true)
    {
        var identificacao = ValidarIdentificacao(codigo, nome, descricao);
        var calculo = identificacao.IsSuccess ? ValidarCalculoPorTaxa(tipoCalculo, taxa, lmiMinimo, lmiMaximo) : identificacao;
        if (calculo.IsFailure)
        {
            return calculo.Error;
        }

        return new CoberturaProduto(codigo, nome, descricao, obrigatoria, tipoCalculo)
        {
            Taxa = taxa,
            LmiMinimo = lmiMinimo,
            LmiMaximo = lmiMaximo,
            PermiteSinistro = permiteSinistro,
        };
    }

    public static Result<CoberturaProduto> ComPremioFixo(
        string codigo,
        string nome,
        string descricao,
        bool obrigatoria,
        decimal premioFixo,
        decimal lmiFixo,
        bool permiteSinistro)
    {
        var identificacao = ValidarIdentificacao(codigo, nome, descricao);
        var calculo = identificacao.IsSuccess ? ValidarPremioFixo(premioFixo, lmiFixo) : identificacao;
        if (calculo.IsFailure)
        {
            return calculo.Error;
        }

        // Faixa degenerada (mín = máx = fixo) mantém a validação de LMI uniforme para todos os tipos.
        return new CoberturaProduto(codigo, nome, descricao, obrigatoria, TipoCalculo.PremioFixo)
        {
            PremioFixo = premioFixo,
            LmiFixo = lmiFixo,
            LmiMinimo = lmiFixo,
            LmiMaximo = lmiFixo,
            PermiteSinistro = permiteSinistro,
        };
    }

    internal void DefinirTaxa(decimal taxa) => Taxa = taxa;

    // Cópia rasa basta: todos os campos são valores ou strings imutáveis.
    internal CoberturaProduto Copiar() => (CoberturaProduto)MemberwiseClone();

    private static Result ValidarCalculoPorTaxa(TipoCalculo tipoCalculo, decimal taxa, decimal lmiMinimo, decimal lmiMaximo)
    {
        if (tipoCalculo is not (TipoCalculo.TaxaSobreLmi or TipoCalculo.TaxaPorMil))
        {
            return ProdutoErros.TipoCalculoInvalido;
        }

        if (!TaxaValida(taxa))
        {
            return ProdutoErros.TaxaInvalida;
        }

        return lmiMinimo < 0 || lmiMaximo < lmiMinimo ? ProdutoErros.LmiInvalido : Result.Success();
    }

    private static Result ValidarPremioFixo(decimal premioFixo, decimal lmiFixo)
    {
        if (premioFixo <= 0)
        {
            return ProdutoErros.PremioFixoInvalido;
        }

        return lmiFixo < 0 ? ProdutoErros.LmiInvalido : Result.Success();
    }

    private static Result ValidarIdentificacao(string codigo, string nome, string descricao)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > CodigoTamanhoMaximo)
        {
            return ProdutoErros.CoberturaCodigoInvalido;
        }

        if (string.IsNullOrWhiteSpace(nome) || nome.Length > NomeTamanhoMaximo)
        {
            return ProdutoErros.CoberturaNomeInvalido;
        }

        return descricao is null || descricao.Length > DescricaoTamanhoMaximo
            ? ProdutoErros.CoberturaDescricaoInvalida
            : Result.Success();
    }
}

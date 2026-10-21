using Apolice.Domain.Comum;

namespace Apolice.Domain.Produtos;

public sealed class Produto : AggregateRoot<string>
{
    private const decimal AliquotaIofMaxima = 1m;
    private const int NomeTamanhoMaximo = 120;
    private const int DescricaoTamanhoMaximo = 1000;

    private readonly List<CoberturaProduto> _coberturas = [];

    private Produto()
    {
        Nome = null!;
        Descricao = null!;
    }

    private Produto(string codigo, string nome, string descricao, decimal aliquotaIof, IEnumerable<CoberturaProduto> coberturas)
        : base(codigo)
    {
        Nome = nome;
        Descricao = descricao;
        AliquotaIof = aliquotaIof;

        // O agregado passa a ser dono exclusivo das coberturas: sem a cópia, a mesma instância reaproveitada
        // em dois produtos (seeder, testes) faria AtualizarTaxa em um alterar o outro.
        _coberturas.AddRange(coberturas.Select(c => c.Copiar()));
    }

    // O código do produto é a própria identidade do agregado (PK de produtos).
    public string Codigo => Id;

    public string Nome { get; private set; }

    public string Descricao { get; private set; }

    public decimal AliquotaIof { get; private set; }

    public IReadOnlyList<CoberturaProduto> Coberturas => _coberturas.AsReadOnly();

    public static Result<Produto> Criar(
        string codigo,
        string nome,
        string descricao,
        decimal aliquotaIof,
        IReadOnlyList<CoberturaProduto> coberturas)
    {
        if (!CodigosProduto.EhValido(codigo))
        {
            return ProdutoErros.CodigoInvalido;
        }

        if (string.IsNullOrWhiteSpace(nome) || nome.Length > NomeTamanhoMaximo
            || descricao is null || descricao.Length > DescricaoTamanhoMaximo)
        {
            return ProdutoErros.IdentificacaoInvalida;
        }

        if (aliquotaIof < 0 || aliquotaIof >= AliquotaIofMaxima)
        {
            return ProdutoErros.AliquotaIofInvalida;
        }

        return ValidarCoberturas(coberturas) is { IsFailure: true } falha
            ? falha.Error
            : new Produto(codigo, nome, descricao, aliquotaIof, coberturas);
    }

    public CoberturaProduto? ObterCobertura(string? codigo) => _coberturas.Find(c => c.Codigo == codigo);

    // "agora" além da assinatura do SPEC 4.2: o evento precisa de OcorridoEm e o tempo é sempre injetado (SPEC 2).
    public Result AtualizarTaxa(string coberturaCodigo, decimal novaTaxa, DateTimeOffset agora)
    {
        var cobertura = ObterCobertura(coberturaCodigo);
        if (cobertura is null)
        {
            return ProdutoErros.CoberturaNaoEncontrada(coberturaCodigo);
        }

        // Mesma taxa não é alteração: sem evento, não há outbox, auditoria nem invalidação de cache à toa.
        var validacao = ValidarNovaTaxa(cobertura, novaTaxa);
        if (validacao.IsFailure || cobertura.Taxa == novaTaxa)
        {
            return validacao;
        }

        var taxaAnterior = cobertura.Taxa;
        cobertura.DefinirTaxa(novaTaxa);
        Raise(new TaxaCoberturaAtualizada(Codigo, cobertura.Codigo, taxaAnterior, novaTaxa, agora));
        return Result.Success();
    }

    private static Result ValidarNovaTaxa(CoberturaProduto cobertura, decimal novaTaxa)
    {
        if (!cobertura.CalculaPorTaxa)
        {
            return ProdutoErros.CoberturaDePremioFixo(cobertura.Codigo);
        }

        return CoberturaProduto.TaxaValida(novaTaxa) ? Result.Success() : ProdutoErros.TaxaInvalida;
    }

    private static Result ValidarCoberturas(IReadOnlyList<CoberturaProduto>? coberturas)
    {
        if (coberturas is null || coberturas.Count == 0)
        {
            return ProdutoErros.SemCoberturas;
        }

        var codigosDistintos = coberturas.Select(c => c.Codigo).Distinct(StringComparer.Ordinal).Count();
        return codigosDistintos == coberturas.Count ? Result.Success() : ProdutoErros.CoberturaDuplicada;
    }
}

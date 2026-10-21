using Apolice.Domain.Comum;

namespace Apolice.Domain.Produtos;

public static class ProdutoErros
{
    public static Error CodigoInvalido { get; } =
        Error.Validacao("Produto.CodigoInvalido", "Código de produto inválido (use AUTO, RESID ou VIDA).");

    public static Error IdentificacaoInvalida { get; } =
        Error.Validacao("Produto.IdentificacaoInvalida", "Nome ou descrição do produto inválidos.");

    public static Error AliquotaIofInvalida { get; } =
        Error.Validacao("Produto.AliquotaIofInvalida", "A alíquota de IOF deve ser uma fração entre 0 e 1.");

    public static Error SemCoberturas { get; } =
        Error.Validacao("Produto.SemCoberturas", "O produto deve ter ao menos uma cobertura.");

    public static Error CoberturaDuplicada { get; } =
        Error.Validacao("Produto.CoberturaDuplicada", "O produto não pode repetir códigos de cobertura.");

    public static Error CoberturaCodigoInvalido { get; } =
        Error.Validacao("Produto.CoberturaCodigoInvalido", "Código de cobertura inválido.");

    public static Error CoberturaNomeInvalido { get; } =
        Error.Validacao("Produto.CoberturaNomeInvalido", "Nome de cobertura inválido.");

    public static Error CoberturaDescricaoInvalida { get; } =
        Error.Validacao("Produto.CoberturaDescricaoInvalida", "Descrição de cobertura inválida.");

    public static Error TipoCalculoInvalido { get; } =
        Error.Validacao("Produto.TipoCalculoInvalido", "Cobertura com taxa exige cálculo TaxaSobreLmi ou TaxaPorMil.");

    public static Error TaxaInvalida { get; } =
        Error.Validacao("Produto.TaxaInvalida", "A taxa deve ser maior que zero e no máximo 1.");

    public static Error PremioFixoInvalido { get; } =
        Error.Validacao("Produto.PremioFixoInvalido", "O prêmio fixo deve ser maior que zero.");

    public static Error LmiInvalido { get; } =
        Error.Validacao("Produto.LmiInvalido", "Limites de LMI inválidos.");

    public static Error CoberturaNaoEncontrada(string? codigo) =>
        Error.NaoEncontrado("Produto.CoberturaNaoEncontrada", $"Cobertura '{codigo}' não encontrada no produto.");

    public static Error CoberturaDePremioFixo(string codigo) =>
        Error.RegraDeNegocio("Produto.CoberturaDePremioFixo", $"A cobertura '{codigo}' tem prêmio fixo e não aceita taxa.");
}

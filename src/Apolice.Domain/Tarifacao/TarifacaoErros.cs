using Apolice.Domain.Comum;

namespace Apolice.Domain.Tarifacao;

public static class TarifacaoErros
{
    public static Error ProdutoIncompativel { get; } =
        Error.Validacao("Tarifacao.ProdutoIncompativel", "Os dados de risco não correspondem ao produto informado.");

    public static Error CepInvalido { get; } =
        Error.Validacao("Tarifacao.CepInvalido", "O CEP deve ter 8 dígitos.");

    public static Error TipoImovelInvalido { get; } =
        Error.Validacao("Tarifacao.TipoImovelInvalido", "Tipo de imóvel inválido (Casa ou Apartamento).");

    public static Error LmiAcimaDoValorImovel { get; } =
        Error.Validacao("Tarifacao.LmiAcimaDoValorImovel", "O LMI de incêndio não pode superar o valor do imóvel.");

    public static Error LmiIpaAcimaDaMorte { get; } =
        Error.Validacao("Tarifacao.LmiIpaAcimaDaMorte", "O LMI de invalidez não pode superar o LMI de morte.");

    public static Error ProdutoNaoSuportado(string codigo) =>
        Error.Validacao("Tarifacao.ProdutoNaoSuportado", $"Não há tarifação para o produto '{codigo}'.");

    public static Error CoberturaInexistente(string? codigo) =>
        Error.Validacao("Tarifacao.CoberturaInexistente", $"A cobertura '{codigo}' não existe no produto.");

    public static Error CoberturaDuplicada(string codigo) =>
        Error.Validacao("Tarifacao.CoberturaDuplicada", $"A cobertura '{codigo}' foi informada mais de uma vez.");

    public static Error CoberturaObrigatoriaAusente(string codigo) =>
        Error.Validacao("Tarifacao.CoberturaObrigatoriaAusente", $"A cobertura obrigatória '{codigo}' não foi informada.");

    public static Error LmiObrigatorio(string codigo) =>
        Error.Validacao("Tarifacao.LmiObrigatorio", $"Informe o LMI da cobertura '{codigo}'.");

    public static Error LmiForaDoLimite(string codigo, decimal minimo, decimal maximo) =>
        Error.Validacao(
            "Tarifacao.LmiForaDoLimite",
            $"O LMI da cobertura '{codigo}' deve estar entre {Dinheiro.Formatar(minimo)} e {Dinheiro.Formatar(maximo)}.");

    public static Error ValorVeiculoForaDoLimite(decimal minimo, decimal maximo) =>
        Error.Validacao(
            "Tarifacao.ValorVeiculoForaDoLimite",
            $"O valor do veículo deve estar entre {Dinheiro.Formatar(minimo)} e {Dinheiro.Formatar(maximo)}.");

    public static Error AnoFabricacaoInvalido(int minimo, int maximo) =>
        Error.Validacao("Tarifacao.AnoFabricacaoInvalido", $"O ano de fabricação deve estar entre {minimo} e {maximo}.");

    public static Error IdadeCondutorForaDoLimite(int minimo, int maximo) =>
        Error.Validacao("Tarifacao.IdadeCondutorForaDoLimite", $"A idade do condutor deve estar entre {minimo} e {maximo} anos.");

    public static Error ClasseBonusInvalida(int minimo, int maximo) =>
        Error.Validacao("Tarifacao.ClasseBonusInvalida", $"A classe de bônus deve estar entre {minimo} e {maximo}.");

    public static Error ValorImovelForaDoLimite(decimal minimo, decimal maximo) =>
        Error.Validacao(
            "Tarifacao.ValorImovelForaDoLimite",
            $"O valor do imóvel deve estar entre {Dinheiro.Formatar(minimo)} e {Dinheiro.Formatar(maximo)}.");

    public static Error ValorMonetarioInvalido(string campo) =>
        Error.Validacao("Tarifacao.ValorMonetarioInvalido", $"O {campo} deve ter no máximo 2 casas decimais.");

    public static Error IdadeSeguradoForaDoLimite(int minimo, int maximo) =>
        Error.Validacao("Tarifacao.IdadeSeguradoForaDoLimite", $"A idade do segurado deve estar entre {minimo} e {maximo} anos.");
}

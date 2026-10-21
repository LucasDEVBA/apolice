using Apolice.Domain.Comum;
using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Apolices;

public static class ApoliceErros
{
    public static Error NumeroInvalido { get; } =
        Error.Validacao("Apolice.NumeroInvalido", "Número de apólice inválido.");

    public static Error EndossoForaDaVigencia { get; } =
        Error.RegraDeNegocio("Apolice.EndossoForaDaVigencia", "A data de efeito do endosso deve ser anterior ao fim da vigência.");

    public static Error LmiInalterado { get; } =
        Error.Validacao("Apolice.LmiInalterado", "O endosso não altera o LMI da cobertura (LMI igual ou fixo).");

    public static Error TipoEndossoInvalido { get; } =
        Error.Validacao("Apolice.TipoEndossoInvalido", "Tipo de endosso inválido.");

    public static Error MotivoCancelamentoInvalido { get; } =
        Error.Validacao("Apolice.MotivoCancelamentoInvalido", "Motivo de cancelamento inválido.");

    public static Error InicioVigenciaInvalido(DateOnly minimo, DateOnly maximo) =>
        Error.Validacao(
            "Apolice.InicioVigenciaInvalido",
            $"O início da vigência deve estar entre {minimo:dd'/'MM'/'yyyy} e {maximo:dd'/'MM'/'yyyy}.");

    public static Error ParcelasInvalidas(int maximo) =>
        Error.Validacao(
            "Apolice.ParcelasInvalidas",
            $"O número de parcelas deve estar entre {ResultadoTarifacao.ParcelasMinimas} e {maximo}.");

    public static Error StatusInvalido(StatusEfetivoApolice status) =>
        Error.RegraDeNegocio("Apolice.StatusInvalido", $"A operação não é permitida para apólice com status {status}.");

    public static Error CoberturaObrigatoria(string codigo) =>
        Error.RegraDeNegocio("Apolice.CoberturaObrigatoria", $"A cobertura obrigatória '{codigo}' não pode ser excluída.");

    public static Error CoberturaJaContratada(string codigo) =>
        Error.RegraDeNegocio("Apolice.CoberturaJaContratada", $"A cobertura '{codigo}' já faz parte da apólice.");

    public static Error CoberturaNaoContratada(string codigo) =>
        Error.RegraDeNegocio("Apolice.CoberturaNaoContratada", $"A cobertura '{codigo}' não faz parte da apólice.");
}

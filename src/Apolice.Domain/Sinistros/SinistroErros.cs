using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public static class SinistroErros
{
    public static Error NumeroInvalido { get; } =
        Error.Validacao("Sinistro.NumeroInvalido", "Número de sinistro inválido.");

    public static Error ValorReclamadoInvalido { get; } =
        Error.Validacao("Sinistro.ValorReclamadoInvalido", "O valor reclamado deve ser maior que zero.");

    public static Error OcorrenciaFutura { get; } =
        Error.Validacao("Sinistro.OcorrenciaFutura", "A data de ocorrência não pode ser futura.");

    public static Error ForaDaVigencia { get; } =
        Error.RegraDeNegocio("Sinistro.ForaDaVigencia", "A ocorrência está fora da vigência da apólice.");

    public static Error AposCancelamento { get; } =
        Error.RegraDeNegocio("Sinistro.AposCancelamento", "A ocorrência é posterior ao cancelamento da apólice.");

    public static Error ValorIndenizacaoInvalido { get; } =
        Error.Validacao("Sinistro.ValorIndenizacaoInvalido", "O valor da indenização deve ser maior que zero.");

    public static Error TriagemInvalida(int scoreMinimo, int scoreMaximo) =>
        Error.Validacao(
            "Sinistro.TriagemInvalida",
            $"A triagem exige score entre {scoreMinimo} e {scoreMaximo} e alertas preenchidos.");

    public static Error DescricaoInvalida(int minimo, int maximo) =>
        Error.Validacao("Sinistro.DescricaoInvalida", $"A descrição deve ter entre {minimo} e {maximo} caracteres.");

    public static Error MotivoNegativaInvalido(int minimo, int maximo) =>
        Error.Validacao("Sinistro.MotivoNegativaInvalido", $"O motivo da negativa deve ter entre {minimo} e {maximo} caracteres.");

    public static Error ObservacaoInvalida(int maximo) =>
        Error.Validacao("Sinistro.ObservacaoInvalida", $"A observação deve ter no máximo {maximo} caracteres.");

    public static Error IndenizacaoAcimaDoLimite(decimal limite) =>
        Error.RegraDeNegocio(
            "Sinistro.IndenizacaoAcimaDoLimite",
            $"A indenização não pode superar {Dinheiro.Formatar(limite)} (menor entre o reclamado e o LMI).");

    public static Error CoberturaNaoContratada(string? codigo) =>
        Error.RegraDeNegocio("Sinistro.CoberturaNaoContratada", $"A cobertura '{codigo}' não faz parte da apólice.");

    public static Error CoberturaSemIndenizacao(string codigo) =>
        Error.RegraDeNegocio("Sinistro.CoberturaSemIndenizacao", $"A cobertura '{codigo}' é um serviço e não admite sinistro.");

    public static Error TransicaoInvalida(StatusSinistro atual, StatusSinistro destino) =>
        Error.RegraDeNegocio("Sinistro.TransicaoInvalida", $"Não é possível passar o sinistro de {atual} para {destino}.");
}

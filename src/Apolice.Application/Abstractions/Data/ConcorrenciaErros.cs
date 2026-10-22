using Apolice.Domain.Comum;

namespace Apolice.Application.Abstractions.Data;

public static class ConcorrenciaErros
{
    public static Error Conflito { get; } =
        Error.Conflito("Concorrencia.Conflito", "O registro foi alterado por outra operação. Recarregue e tente novamente.");
}

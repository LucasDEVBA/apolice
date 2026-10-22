namespace Apolice.Application.Common;

internal static class Releitura
{
    // Comandos devolvem o DTO relido do lado de leitura (o mesmo do GET). Não achar o que acabou de
    // ser gravado é falha de infraestrutura ou bug, não um fluxo previsível — por isso exceção.
    public static T Obrigatoria<T>(T? dto, string entidade)
        where T : class =>
        dto ?? throw new InvalidOperationException($"O registro de {entidade} recém-gravado não foi encontrado na releitura.");
}

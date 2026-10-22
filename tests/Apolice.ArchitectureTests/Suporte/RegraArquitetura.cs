using ArchUnitNET.Fluent;

namespace Apolice.ArchitectureTests.Suporte;

internal static class RegraArquitetura
{
    // Evaluate em vez de HasNoViolations: a falha lista cada tipo/membro infrator, não só "false".
    // A regra também falha quando o "That()" não seleciona nada (exigência padrão de resultado positivo),
    // o que impede um filtro quebrado de passar sem verificar coisa alguma.
    public static void DeveSerRespeitada(this IArchRule regra)
    {
        var violacoes = regra.Evaluate(Solucao.Arquitetura)
            .Where(resultado => !resultado.Passed)
            .Select(resultado => resultado.Description)
            .ToList();

        violacoes.ShouldBeEmpty(regra.Description);
    }
}

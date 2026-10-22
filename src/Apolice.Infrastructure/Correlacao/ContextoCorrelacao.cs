namespace Apolice.Infrastructure.Correlacao;

// Um por escopo de DI (requisição HTTP ou mensagem consumida): a API o preenche com o X-Correlation-Id e o Worker
// com o correlationId da mensagem. Outbox e auditoria leem daqui, sem depender de HttpContext.
public sealed class ContextoCorrelacao
{
    public const int TamanhoMaximo = 64;

    public string? CorrelationId { get; private set; }

    // Quem preenche já validou o formato (a API aceita só [A-Za-z0-9-]); o corte aqui só protege a coluna.
    public void Definir(string? correlationId) =>
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? null
            : correlationId.Length > TamanhoMaximo ? correlationId[..TamanhoMaximo] : correlationId;
}

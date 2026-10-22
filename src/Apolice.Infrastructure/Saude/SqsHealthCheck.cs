using System.Net;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Apolice.Infrastructure.Configuracao;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Apolice.Infrastructure.Saude;

// A role da API só tem sqs:SendMessage (privilégio mínimo, infra/terraform/iam.tf): um AccessDenied assinado pelo
// SQS já prova rede, endpoint e credenciais válidas, então conta como saudável. Qualquer outra falha não.
internal sealed class SqsHealthCheck(IAmazonSQS sqs, IOptions<MessagingOptions> opcoes) : IHealthCheck
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(Limite);
        try
        {
            await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest { QueueUrl = opcoes.Value.Sqs.QueueUrl, AttributeNames = ["QueueArn"] }, prazo.Token);
            return HealthCheckResult.Healthy("Fila SQS acessível.");
        }
        catch (AmazonSQSException ex) when (ex.StatusCode == HttpStatusCode.Forbidden || ex.ErrorCode == "AccessDenied")
        {
            return HealthCheckResult.Healthy("Fila SQS alcançável (sem permissão de leitura de atributos, como esperado na API).");
        }
        catch (Exception ex) when (ex is AmazonServiceException or HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Fila SQS indisponível.", ex);
        }
    }
}

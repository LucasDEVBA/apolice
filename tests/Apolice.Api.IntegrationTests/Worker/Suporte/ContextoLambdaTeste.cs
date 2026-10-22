using Amazon.Lambda.Core;

namespace Apolice.Api.IntegrationTests.Worker.Suporte;

// Contexto mínimo de invocação: o handler só usa RemainingTime (prazo) — o resto existe para cumprir a interface.
internal sealed class ContextoLambdaTeste(TimeSpan restante) : ILambdaContext
{
    public static readonly TimeSpan PrazoFolgado = TimeSpan.FromSeconds(60);

    public string AwsRequestId { get; } = Guid.NewGuid().ToString();

    public IClientContext ClientContext => null!;

    public string FunctionName => "apolice-worker-teste";

    public string FunctionVersion => "$LATEST";

    public ICognitoIdentity Identity => null!;

    public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:000000000000:function:apolice-worker-teste";

    public ILambdaLogger Logger { get; } = new LoggerNulo();

    public string LogGroupName => "/aws/lambda/apolice-worker-teste";

    public string LogStreamName => "teste";

    public int MemoryLimitInMB => 512;

    public TimeSpan RemainingTime { get; } = restante;

    public static ContextoLambdaTeste Folgado() => new(PrazoFolgado);

    private sealed class LoggerNulo : ILambdaLogger
    {
        public void Log(string message)
        {
        }

        public void LogLine(string message)
        {
        }
    }
}

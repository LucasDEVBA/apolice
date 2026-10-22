using Apolice.Worker.Composicao;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Apolice.Worker.Lambda;

// Um host por container da Lambda, criado no primeiro uso (cold start) e reaproveitado nas invocações seguintes.
// Falha na criação (SSM indisponível, options inválidas) não fica em cache: a próxima invocação tenta de novo, em vez
// de o container ficar envenenado até ser reciclado.
internal static class ProvedorLambda
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static IHost? _host;

    public static async ValueTask<IServiceProvider> ObterAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _host) is { } pronto)
        {
            return pronto.Services;
        }

        await Trava.WaitAsync(cancellationToken);
        try
        {
            if (_host is { } criado)
            {
                return criado.Services;
            }

            var host = await IniciarAsync(cancellationToken);
            Volatile.Write(ref _host, host);
            return host.Services;
        }
        finally
        {
            Trava.Release();
        }
    }

    // StartAsync valida as options (ValidateOnStart) e liga os providers do OpenTelemetry; nenhum hosted service de
    // trabalho é registrado aqui — quem trabalha é o handler da invocação.
    private static async Task<IHost> IniciarAsync(CancellationToken cancellationToken)
    {
        var builder = ComposicaoWorker.CriarBuilder(new HostApplicationBuilderSettings { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddSingleton<IHostLifetime, VidaLambda>();
        var host = builder.Build();
        try
        {
            await host.StartAsync(cancellationToken);
            return host;
        }
        catch
        {
            await DescartarAsync(host);
            throw;
        }
    }

    private static async ValueTask DescartarAsync(IHost host)
    {
        if (host is IAsyncDisposable assincrono)
        {
            await assincrono.DisposeAsync();
            return;
        }

        host.Dispose();
    }

    // O ConsoleLifetime padrão captura SIGTERM e espera um StopAsync que ninguém chama dentro da Lambda; aqui o ciclo de
    // vida é do runtime da AWS.
    private sealed class VidaLambda : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

// O ambiente da Lambda congela entre invocações: o que estiver no buffer dos exportadores sairia só na próxima (ou
// nunca, se o container for reciclado). Sem exportador configurado, o flush não tem o que fazer. O flush cabe no tempo
// que resta da invocação: estourar o timeout perderia a resposta (no SQS, o lote inteiro seria reentregue).
internal static class TelemetriaLambda
{
    private const int Provedores = 2;
    private static readonly TimeSpan PrazoMaximoPorProvedor = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FolgaResposta = TimeSpan.FromMilliseconds(500);

    public static void Descarregar(IServiceProvider servicos, TimeSpan restante)
    {
        var porProvedor = (restante - FolgaResposta) / Provedores;
        if (porProvedor <= TimeSpan.Zero)
        {
            return;
        }

        var prazoMs = (int)Math.Min(PrazoMaximoPorProvedor.TotalMilliseconds, porProvedor.TotalMilliseconds);
        servicos.GetService<TracerProvider>()?.ForceFlush(prazoMs);
        servicos.GetService<MeterProvider>()?.ForceFlush(prazoMs);
    }
}

internal static class PrazoLambda
{
    // Folga para devolver a resposta (falhas parciais) antes do timeout: estourar o prazo faria o SQS reentregar o lote
    // inteiro, inclusive o que já foi processado.
    private static readonly TimeSpan Margem = TimeSpan.FromSeconds(3);

    public static TimeSpan Util(TimeSpan restante) => restante > Margem ? restante - Margem : TimeSpan.Zero;
}

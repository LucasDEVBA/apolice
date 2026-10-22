using Apolice.Infrastructure.Persistence.Migracao;
using Apolice.Infrastructure.Persistence.Seed;
using Apolice.Worker.Composicao;
using Serilog;
using Serilog.Formatting.Compact;

namespace Apolice.Worker;

// Um binário, três papéis: "migrate" e "seed" (one-shot, no deploy e no docker compose), host local sem argumentos
// (relay + consumidor RabbitMQ) e, na AWS, os handlers de Lambda em Lambda/ (que não passam por aqui).
public static class Program
{
    public const string VerboMigrate = "migrate";
    public const string VerboSeed = "seed";

    private const int CodigoSucesso = 0;
    private const int CodigoFalha = 1;
    private const int CodigoUsoInvalido = 2;

    public static async Task<int> Main(string[] args)
    {
        // Logger mínimo só para o que acontece antes do host existir (configuração inválida, SSM fora): o host tem o
        // próprio, configurado pela composição.
        Log.Logger = new LoggerConfiguration().WriteTo.Console(new RenderedCompactJsonFormatter()).CreateLogger();
        try
        {
            return await ExecutarAsync(args);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Fatal(ex, "Worker encerrado por falha: {Verbo}", args.FirstOrDefault() ?? "host");
            return CodigoFalha;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static Task<int> ExecutarAsync(string[] args) =>
        args.FirstOrDefault() switch
        {
            null => HospedarAsync(args),
            VerboMigrate => ExecutarVerboAsync(args[1..], (sp, ct) => sp.GetRequiredService<IMigradorBanco>().MigrarAsync(ct)),
            VerboSeed => ExecutarVerboAsync(args[1..], (sp, ct) => sp.GetRequiredService<DataSeeder>().SemearAsync(ct)),
            var desconhecido => UsoInvalidoAsync(desconhecido),
        };

    private static async Task<int> HospedarAsync(string[] args)
    {
        var builder = ComposicaoWorker.CriarBuilder(Configuracoes(args));
        ComposicaoWorker.AdicionarHostLocal(builder);

        // RunAsync cuida do SIGTERM/Ctrl+C: cancela os hosted services e espera até HostOptions.ShutdownTimeout.
        await builder.Build().RunAsync();
        return CodigoSucesso;
    }

    // O host é iniciado também nos verbos: ValidateOnStart roda (configuração errada falha antes de tocar o banco) e o
    // Ctrl+C/SIGTERM vira cancelamento cooperativo via ApplicationStopping.
    private static async Task<int> ExecutarVerboAsync(string[] args, Func<IServiceProvider, CancellationToken, Task> verbo)
    {
        var builder = ComposicaoWorker.CriarBuilder(Configuracoes(args));
        builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
        var host = builder.Build();
        try
        {
            await host.StartAsync();
            var parada = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            await using (var escopo = host.Services.CreateAsyncScope())
            {
                await verbo(escopo.ServiceProvider, parada);
            }

            await host.StopAsync(CancellationToken.None);
            return CodigoSucesso;
        }
        finally
        {
            await DescartarAsync(host);
        }
    }

    // Content root na pasta do binário: os appsettings*.json publicados valem qualquer que seja o diretório atual
    // (docker, runner do deploy, "dotnet Apolice.Worker.dll" de outra pasta).
    private static HostApplicationBuilderSettings Configuracoes(string[] args) =>
        new() { Args = args, ContentRootPath = AppContext.BaseDirectory };

    private static Task<int> UsoInvalidoAsync(string verbo)
    {
        Log.Error("Verbo desconhecido '{Verbo}'. Uso: Apolice.Worker [{Migrate}|{Seed}] (sem argumentos: host local).", verbo, VerboMigrate, VerboSeed);
        return Task.FromResult(CodigoUsoInvalido);
    }

    // Alguns serviços da infraestrutura só têm DisposeAsync (conexão RabbitMQ): o Dispose síncrono do provider falharia.
    private static async ValueTask DescartarAsync(IHost host)
    {
        if (host is IAsyncDisposable assincrono)
        {
            await assincrono.DisposeAsync();
            return;
        }

        host.Dispose();
    }
}

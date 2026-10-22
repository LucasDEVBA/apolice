using Apolice.Infrastructure.Configuracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apolice.Infrastructure.Persistence.Migracao;

// Ponto único do verbo "migrate" do Worker: escolhe o caminho pelo Database:Provider.
public interface IMigradorBanco
{
    Task MigrarAsync(CancellationToken cancellationToken);
}

internal sealed partial class MigradorBanco(
    ApoliceDbContext db,
    DsqlMigrationRunner dsql,
    IOptions<DatabaseOptions> opcoes,
    ILogger<MigradorBanco> logger) : IMigradorBanco
{
    public async Task MigrarAsync(CancellationToken cancellationToken)
    {
        if (opcoes.Value.Provider == ProvedorBanco.Dsql)
        {
            await dsql.AplicarAsync(cancellationToken);
            return;
        }

        var pendentes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        await db.Database.MigrateAsync(cancellationToken);
        LogMigrado(logger, pendentes.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PostgreSQL migrado: {Quantidade} migration(s) aplicada(s).")]
    private static partial void LogMigrado(ILogger logger, int quantidade);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Apolice.Infrastructure.Persistence;

// Só para o "dotnet ef migrations": as migrations são geradas com o provedor Postgres (o DSQL fala o mesmo dialeto
// e o DsqlMigrationRunner adapta o DDL). Nenhuma conexão é aberta para gerar migration; a connection string é fictícia
// de propósito e, para "dotnet ef database update" local, pode ser trocada por APOLICE_DESIGN_CONNECTION.
internal sealed class ApoliceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApoliceDbContext>
{
    private const string ConnectionStringPadrao = "Host=localhost;Port=5432;Database=apolice;Username=apolice";

    public ApoliceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APOLICE_DESIGN_CONNECTION") ?? ConnectionStringPadrao;
        var options = new DbContextOptionsBuilder<ApoliceDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ApoliceDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApoliceDbContext(options);
    }
}

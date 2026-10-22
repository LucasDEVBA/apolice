using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Apolice.Api.IntegrationTests.Infraestrutura.Suporte.PostgresFixture))]

namespace Apolice.Api.IntegrationTests.Infraestrutura.Suporte;

// Um container PostgreSQL para a assembly inteira (a máquina de CI e a local têm pouca memória) e um banco novo por
// classe de teste: isolamento sem pagar a subida do container de novo.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private int _bancos;

    public async ValueTask InitializeAsync() => await _container.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<string> CriarBancoAsync(string prefixo)
    {
        var nome = $"{prefixo.ToLowerInvariant()}_{Interlocked.Increment(ref _bancos)}";
        await using var conexao = new NpgsqlConnection(_container.GetConnectionString());
        await conexao.OpenAsync(TestContext.Current.CancellationToken);
        await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{nome}\"", conexao);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = nome }.ConnectionString;
    }
}

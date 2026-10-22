using Apolice.Application.Abstractions.Data;
using Apolice.Infrastructure.Persistence.Provedores;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apolice.Infrastructure.Persistence;

// Upsert atômico numa conexão própria (autocommit), fora do SaveChanges do caso de uso: o número fica confirmado
// mesmo que a emissão falhe depois (lacuna aceita pelo SPEC 6). No PostgreSQL o ON CONFLICT serializa pelo lock da
// linha; no DSQL não há lock e a disputa pela mesma chave aparece como conflito de OCC no commit, então refaz.
internal sealed partial class Numerador(NpgsqlDataSource fonte, ILogger<Numerador> logger) : INumerador
{
    private const int TentativasMaximas = 8;
    private const int EsperaBaseMs = 20;
    private const int EsperaMaximaMs = 1_000;

    private const string Sql = """
        INSERT INTO numeradores (chave, ultimo) VALUES (@Chave, 1)
        ON CONFLICT (chave) DO UPDATE SET ultimo = numeradores.ultimo + 1
        RETURNING ultimo
        """;

    public async Task<long> ProximoAsync(string chave, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
                return await conexao.ExecuteScalarAsync<long>(new CommandDefinition(Sql, new { Chave = chave }, cancellationToken: cancellationToken));
            }
            catch (PostgresException ex) when (ErrosTransitorios.EhConflitoOcc(ex) && tentativa < TentativasMaximas)
            {
                LogConflito(logger, chave, tentativa);
                await Task.Delay(Espera(tentativa), cancellationToken);
            }
        }
    }

    // Exponencial com jitter: concorrentes na mesma chave não voltam todos no mesmo instante.
    private static TimeSpan Espera(int tentativa)
    {
        var teto = Math.Min(EsperaMaximaMs, EsperaBaseMs * (1 << tentativa));
        return TimeSpan.FromMilliseconds(Random.Shared.Next(teto / 2, teto));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Conflito de concorrência no numerador {Chave} (tentativa {Tentativa}); refazendo.")]
    private static partial void LogConflito(ILogger logger, string chave, int tentativa);
}

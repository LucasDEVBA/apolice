using Apolice.Infrastructure.Persistence.Registros;
using Dapper;
using Npgsql;

namespace Apolice.Infrastructure.Idempotencia;

// Respostas gravadas por Idempotency-Key (SPEC 7), para o filtro da API. Fora do UoW de propósito: a resposta só é
// registrada depois que o caso de uso já fez commit, e um registro concorrente com a mesma chave perde (ON CONFLICT).
public interface IArmazemIdempotencia
{
    Task<RespostaIdempotente?> ObterAsync(Guid usuarioId, string chave, DateTimeOffset agora, CancellationToken cancellationToken);

    // false = outra requisição com a mesma chave registrou primeiro (e ainda vale).
    Task<bool> RegistrarAsync(Guid usuarioId, string chave, RespostaIdempotente resposta, DateTimeOffset agora, CancellationToken cancellationToken);
}

// HashRequisicao: SHA-256 hex do corpo da requisição (mesma chave + corpo diferente → 422, decisão da API).
public sealed record RespostaIdempotente(string HashRequisicao, int StatusCode, string? ContentType, string Corpo);

internal sealed class ArmazemIdempotencia(NpgsqlDataSource fonte) : IArmazemIdempotencia
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    public async Task<RespostaIdempotente?> ObterAsync(Guid usuarioId, string chave, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        const string Sql = """
            SELECT hash_requisicao AS HashRequisicao, status_code AS StatusCode, content_type AS ContentType, corpo AS Corpo
            FROM idempotencia
            WHERE usuario_id = @UsuarioId AND chave = @Chave AND expira_em > @Agora
            """;
        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var linha = await conexao.QueryFirstOrDefaultAsync<LinhaIdempotencia>(
            new CommandDefinition(Sql, new { UsuarioId = usuarioId, Chave = chave, Agora = agora.ToUniversalTime() }, cancellationToken: cancellationToken));
        return linha is null ? null : new RespostaIdempotente(linha.HashRequisicao, linha.StatusCode, linha.ContentType, linha.Corpo);
    }

    // Registro vencido é substituído (a chave pode ser reutilizada depois de 24 h); válido nunca é sobrescrito.
    public async Task<bool> RegistrarAsync(Guid usuarioId, string chave, RespostaIdempotente resposta, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        if (chave.Length > IdempotenciaRegistro.TamanhoMaximoChave)
        {
            throw new ArgumentException($"A chave de idempotência tem no máximo {IdempotenciaRegistro.TamanhoMaximoChave} caracteres.", nameof(chave));
        }

        const string Sql = """
            INSERT INTO idempotencia (usuario_id, chave, hash_requisicao, status_code, content_type, corpo, criado_em, expira_em)
            VALUES (@UsuarioId, @Chave, @HashRequisicao, @StatusCode, @ContentType, @Corpo, @Agora, @ExpiraEm)
            ON CONFLICT (usuario_id, chave) DO UPDATE
               SET hash_requisicao = EXCLUDED.hash_requisicao, status_code = EXCLUDED.status_code, content_type = EXCLUDED.content_type,
                   corpo = EXCLUDED.corpo, criado_em = EXCLUDED.criado_em, expira_em = EXCLUDED.expira_em
             WHERE idempotencia.expira_em <= EXCLUDED.criado_em
            """;
        var instante = agora.ToUniversalTime();
        await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
        var linhas = await conexao.ExecuteAsync(new CommandDefinition(
            Sql,
            new
            {
                UsuarioId = usuarioId,
                Chave = chave,
                resposta.HashRequisicao,
                resposta.StatusCode,
                resposta.ContentType,
                resposta.Corpo,
                Agora = instante,
                ExpiraEm = instante.Add(Validade),
            },
            cancellationToken: cancellationToken));
        return linhas > 0;
    }

    private sealed class LinhaIdempotencia
    {
        public string HashRequisicao { get; init; } = string.Empty;

        public int StatusCode { get; init; }

        public string? ContentType { get; init; }

        public string Corpo { get; init; } = string.Empty;
    }
}

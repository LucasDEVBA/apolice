using Apolice.Infrastructure.Persistence.Provedores;
using Apolice.Infrastructure.Persistence.Registros;
using Dapper;
using Npgsql;

namespace Apolice.Infrastructure.Idempotencia;

// Respostas gravadas por Idempotency-Key (SPEC 7), para o filtro da API. Fora do UoW de propósito: a chave é RESERVADA
// antes do caso de uso (INSERT ... ON CONFLICT: só uma requisição por usuário + chave executa) e a resposta 2xx é
// gravada depois do commit. Sem a reserva, duas requisições simultâneas com a mesma chave executariam ambas.
public interface IArmazemIdempotencia
{
    Task<RespostaIdempotente?> ObterAsync(Guid usuarioId, string chave, DateTimeOffset agora, CancellationToken cancellationToken);

    // Reservada = esta requisição executa; EmProcessamento/Concluida = outra já tem a chave (Resposta traz o hash dela).
    Task<ReservaIdempotencia> ReservarAsync(Guid usuarioId, string chave, string hashRequisicao, DateTimeOffset agora, CancellationToken cancellationToken);

    // Grava a resposta na reserva desta requisição (mesmo hash, ainda pendente); false = a reserva não existe mais.
    Task<bool> ConcluirAsync(Guid usuarioId, string chave, RespostaIdempotente resposta, DateTimeOffset agora, CancellationToken cancellationToken);

    // Desfaz a reserva pendente (o caso de uso falhou): erro não consome a chave.
    Task LiberarAsync(Guid usuarioId, string chave, CancellationToken cancellationToken);

    // Apaga até "lote" registros vencidos; devolve quantos saíram (o corpo guarda CPF e e-mail: não fica além das 24 h).
    Task<int> PurgarVencidosAsync(DateTimeOffset agora, int lote, CancellationToken cancellationToken);
}

// HashRequisicao: SHA-256 hex do corpo da requisição (mesma chave + corpo diferente → 422, decisão da API).
public sealed record RespostaIdempotente(string HashRequisicao, int StatusCode, string? ContentType, string Corpo);

public enum EstadoReservaIdempotencia
{
    Reservada,
    EmProcessamento,
    Concluida,
}

public sealed record ReservaIdempotencia(EstadoReservaIdempotencia Estado, RespostaIdempotente? Existente);

internal sealed class ArmazemIdempotencia(NpgsqlDataSource fonte) : IArmazemIdempotencia
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    // Maior que o timeout da requisição (Lambda: 30 s): uma reserva órfã (processo morto no meio) libera a chave sozinha.
    public static readonly TimeSpan ValidadeReserva = TimeSpan.FromMinutes(2);

    // status_code 0 marca a reserva pendente: nenhuma resposta HTTP real tem esse código.
    private const int StatusPendente = 0;
    private const int TentativasMaximas = 5;
    private const int EsperaBaseMs = 20;

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

    // Registro vencido é substituído (a chave pode ser reutilizada depois de 24 h); válido nunca é sobrescrito. Entre o
    // INSERT perdido e a leitura o registro pode vencer ou ser liberado: aí a reserva é tentada de novo.
    public async Task<ReservaIdempotencia> ReservarAsync(Guid usuarioId, string chave, string hashRequisicao, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ValidarChave(chave);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashRequisicao);

        const string Sql = """
            INSERT INTO idempotencia (usuario_id, chave, hash_requisicao, status_code, content_type, corpo, criado_em, expira_em)
            VALUES (@UsuarioId, @Chave, @HashRequisicao, @Status, NULL, '', @Agora, @ExpiraEm)
            ON CONFLICT (usuario_id, chave) DO UPDATE
               SET hash_requisicao = EXCLUDED.hash_requisicao, status_code = EXCLUDED.status_code, content_type = NULL,
                   corpo = '', criado_em = EXCLUDED.criado_em, expira_em = EXCLUDED.expira_em
             WHERE idempotencia.expira_em <= EXCLUDED.criado_em
            """;
        var instante = agora.ToUniversalTime();
        var parametros = new { UsuarioId = usuarioId, Chave = chave, HashRequisicao = hashRequisicao, Status = StatusPendente, Agora = instante, ExpiraEm = instante.Add(ValidadeReserva) };
        for (var tentativa = 1; ; tentativa++)
        {
            if (await ExecutarAsync(Sql, parametros, cancellationToken) > 0)
            {
                return new ReservaIdempotencia(EstadoReservaIdempotencia.Reservada, null);
            }

            if (await ObterAsync(usuarioId, chave, agora, cancellationToken) is { } existente)
            {
                var estado = existente.StatusCode == StatusPendente ? EstadoReservaIdempotencia.EmProcessamento : EstadoReservaIdempotencia.Concluida;
                return new ReservaIdempotencia(estado, existente);
            }

            if (tentativa >= TentativasMaximas)
            {
                throw new InvalidOperationException("Não foi possível reservar a Idempotency-Key: o registro mudou a cada tentativa.");
            }
        }
    }

    public async Task<bool> ConcluirAsync(Guid usuarioId, string chave, RespostaIdempotente resposta, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        ValidarChave(chave);

        const string Sql = """
            UPDATE idempotencia
               SET status_code = @StatusCode, content_type = @ContentType, corpo = @Corpo, expira_em = @ExpiraEm
             WHERE usuario_id = @UsuarioId AND chave = @Chave AND hash_requisicao = @HashRequisicao AND status_code = @Pendente
            """;
        var parametros = new
        {
            UsuarioId = usuarioId,
            Chave = chave,
            resposta.HashRequisicao,
            resposta.StatusCode,
            resposta.ContentType,
            resposta.Corpo,
            Pendente = StatusPendente,
            ExpiraEm = agora.ToUniversalTime().Add(Validade),
        };
        return await ExecutarAsync(Sql, parametros, cancellationToken) > 0;
    }

    public async Task LiberarAsync(Guid usuarioId, string chave, CancellationToken cancellationToken)
    {
        const string Sql = "DELETE FROM idempotencia WHERE usuario_id = @UsuarioId AND chave = @Chave AND status_code = @Pendente";
        await ExecutarAsync(Sql, new { UsuarioId = usuarioId, Chave = chave, Pendente = StatusPendente }, cancellationToken);
    }

    // Em lotes pequenos: o DSQL limita as linhas alteradas por transação.
    public async Task<int> PurgarVencidosAsync(DateTimeOffset agora, int lote, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lote, 1);

        const string Sql = """
            DELETE FROM idempotencia
             WHERE (usuario_id, chave) IN (SELECT usuario_id, chave FROM idempotencia WHERE expira_em <= @Agora LIMIT @Lote)
            """;
        return await ExecutarAsync(Sql, new { Agora = agora.ToUniversalTime(), Lote = lote }, cancellationToken);
    }

    private static void ValidarChave(string chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        if (chave.Length > IdempotenciaRegistro.TamanhoMaximoChave)
        {
            throw new ArgumentException($"A chave de idempotência tem no máximo {IdempotenciaRegistro.TamanhoMaximoChave} caracteres.", nameof(chave));
        }
    }

    // Autocommit numa conexão própria. No DSQL dois INSERTs concorrentes da mesma chave não bloqueiam: o perdedor recebe
    // conflito de OCC no commit e, refeito, passa a ver a linha do vencedor.
    private async Task<int> ExecutarAsync(string sql, object parametros, CancellationToken cancellationToken)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
                return await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
            }
            catch (PostgresException ex) when (ErrosTransitorios.EhConflitoOcc(ex) && tentativa < TentativasMaximas)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(EsperaBaseMs * tentativa), cancellationToken);
            }
        }
    }

    private sealed class LinhaIdempotencia
    {
        public string HashRequisicao { get; init; } = string.Empty;

        public int StatusCode { get; init; }

        public string? ContentType { get; init; }

        public string Corpo { get; init; } = string.Empty;
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Comum;
using Apolice.Infrastructure.Idempotencia;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Apolice.Api.Http;

// SPEC 7: Idempotency-Key opcional (≤ 100) em POST /apolices e POST /sinistros. Mesma chave + mesmo usuário + mesmo
// corpo → devolve a resposta gravada sem reexecutar; corpo diferente → 422. A chave é reservada ANTES do caso de uso:
// uma segunda requisição simultânea com a mesma chave recebe 409 (Retry-After) em vez de executar de novo. Só respostas
// 2xx ficam gravadas: erro libera a reserva e não "consome" a chave, e o cliente pode corrigir e tentar de novo.
internal sealed partial class IdempotencyFilter<T>(
    IArmazemIdempotencia armazem,
    ICurrentUser usuario,
    TimeProvider tempo,
    HashRequisicao hashes,
    IOptions<JsonOptions> json,
    ILogger<IdempotencyFilter<T>> logger) : IEndpointFilter
    where T : class
{
    private const char PrimeiroCaractereVisivel = '!';
    private const char UltimoCaractereVisivel = '~';
    private const string TipoJson = "application/json; charset=utf-8";
    private const int SegundosParaRepetir = 1;

    private static readonly Error ChaveInvalida = Error.Validacao(
        "Idempotencia.ChaveInvalida", $"O header Idempotency-Key deve ter de 1 a {CabecalhosApi.TamanhoMaximoIdempotencyKey} caracteres visíveis, sem espaços.");

    private static readonly Error CorpoDiferente = Error.RegraDeNegocio(
        "Idempotencia.CorpoDiferente", "Esta Idempotency-Key já foi usada com outro corpo de requisição. Gere uma nova chave.");

    private static readonly Error EmProcessamento = Error.Conflito(
        "Idempotencia.EmProcessamento", "Uma requisição com esta Idempotency-Key ainda está em processamento. Tente de novo em instantes.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var cabecalho = http.Request.Headers[CabecalhosApi.IdempotencyKey];
        if (cabecalho.Count == 0)
        {
            return await next(context);
        }

        var chave = cabecalho.ToString();
        if (cabecalho.Count > 1 || !ChaveValida(chave))
        {
            return Problemas.De(ChaveInvalida);
        }

        // A política de autorização já exigiu usuário; sem id não há a quem atrelar a chave.
        if (usuario.UsuarioId is not { } usuarioId || context.Arguments.OfType<T>().FirstOrDefault() is not { } corpo)
        {
            return await next(context);
        }

        var hash = hashes.Calcular(corpo);
        var reserva = await armazem.ReservarAsync(usuarioId, chave, hash, tempo.GetUtcNow(), http.RequestAborted);
        return reserva switch
        {
            { Estado: EstadoReservaIdempotencia.Reservada } => await ExecutarAsync(context, next, usuarioId, chave, hash),
            { Existente: { } existente } when !string.Equals(existente.HashRequisicao, hash, StringComparison.Ordinal) => Problemas.De(CorpoDiferente),
            { Estado: EstadoReservaIdempotencia.Concluida, Existente: { } concluida } => Repetir(concluida, http),
            _ => AindaEmProcessamento(http),
        };
    }

    private async Task<object?> ExecutarAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next, Guid usuarioId, string chave, string hash)
    {
        object? resultado;
        try
        {
            resultado = await next(context);
        }
        catch
        {
            await LiberarAsync(usuarioId, chave);
            throw;
        }

        if (Sucesso(resultado) is { } sucesso)
        {
            await ConcluirAsync(usuarioId, chave, hash, sucesso);
        }
        else
        {
            await LiberarAsync(usuarioId, chave);
        }

        return resultado;
    }

    private static bool ChaveValida(string chave) =>
        chave.Length is > 0 and <= CabecalhosApi.TamanhoMaximoIdempotencyKey && chave.All(c => c is >= PrimeiroCaractereVisivel and <= UltimoCaractereVisivel);

    // 201 repetido devolve também o Location: as rotas idempotentes criam o recurso em "{coleção}/{id}".
    private static IResult Repetir(RespostaIdempotente gravada, HttpContext http)
    {
        http.Response.Headers[CabecalhosApi.RespostaRepetida] = "true";
        if (gravada.StatusCode == StatusCodes.Status201Created && IdDoCorpo(gravada.Corpo) is { } id)
        {
            http.Response.Headers.Location = $"{http.Request.PathBase}{http.Request.Path.Value?.TrimEnd('/')}/{id}";
        }

        return Results.Text(gravada.Corpo, gravada.ContentType, Encoding.UTF8, gravada.StatusCode);
    }

    private static IResult AindaEmProcessamento(HttpContext http)
    {
        http.Response.Headers.RetryAfter = SegundosParaRepetir.ToString(CultureInfo.InvariantCulture);
        return Problemas.De(EmProcessamento);
    }

    private static string? IdDoCorpo(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        return documento.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    // O caso de uso já fez commit: falhar aqui não pode virar erro para o cliente (ele repetiria e duplicaria a
    // operação). Por isso CancellationToken.None e, na falha, só o log; a reserva pendente segue protegendo a chave até
    // vencer, e a resposta sai normal.
    private async Task ConcluirAsync(Guid usuarioId, string chave, string hash, (int Status, object Valor) sucesso)
    {
        try
        {
            var corpo = JsonSerializer.Serialize(sucesso.Valor, sucesso.Valor.GetType(), json.Value.SerializerOptions);
            var resposta = new RespostaIdempotente(hash, sucesso.Status, TipoJson, corpo);
            if (!await armazem.ConcluirAsync(usuarioId, chave, resposta, tempo.GetUtcNow(), CancellationToken.None))
            {
                LogReservaPerdida(logger, usuarioId);
            }
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaAoGravar(logger, excecao, usuarioId);
        }
    }

    // Best-effort: sem a liberação a chave só fica presa até a reserva vencer (minutos), nunca 24 h.
    private async Task LiberarAsync(Guid usuarioId, string chave)
    {
        try
        {
            await armazem.LiberarAsync(usuarioId, chave, CancellationToken.None);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaAoLiberar(logger, excecao, usuarioId);
        }
    }

    private static (int Status, object Valor)? Sucesso(object? resultado)
    {
        var atual = resultado is INestedHttpResult aninhado ? aninhado.Result : resultado;
        return atual is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices } comStatus
            && atual is IValueHttpResult { Value: { } valor }
            ? (comStatus.StatusCode.Value, valor)
            : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reserva da Idempotency-Key do usuário {UsuarioId} venceu antes de gravar a resposta.")]
    private static partial void LogReservaPerdida(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao gravar a resposta idempotente do usuário {UsuarioId}; a operação já foi concluída.")]
    private static partial void LogFalhaAoGravar(ILogger logger, Exception excecao, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao liberar a Idempotency-Key do usuário {UsuarioId}; ela vence sozinha em minutos.")]
    private static partial void LogFalhaAoLiberar(ILogger logger, Exception excecao, Guid usuarioId);
}

// Hash do corpo na forma canônica: o objeto já desserializado, reserializado (espaços e ordem dos campos não contam) e
// com os decimais normalizados (100, 100.0 e 1e2 são o mesmo valor; o decimal guarda a escala e serializaria diferente).
internal sealed class HashRequisicao
{
    private readonly JsonSerializerOptions _opcoes;

    public HashRequisicao(IOptions<JsonOptions> json)
    {
        ArgumentNullException.ThrowIfNull(json);
        _opcoes = new JsonSerializerOptions(json.Value.SerializerOptions);
        _opcoes.Converters.Insert(0, new DecimalCanonicoConverter());
        _opcoes.MakeReadOnly(populateMissingResolver: true);
    }

    public string Calcular(object corpo)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(corpo, corpo.GetType(), _opcoes)));
    }

    private sealed class DecimalCanonicoConverter : JsonConverter<decimal>
    {
        // Dividir por 1 com 28+ casas remove os zeros à direita da escala sem mudar o valor.
        private const decimal UmComEscalaMaxima = 1.000000000000000000000000000000000m;

        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteNumberValue(value / UmComEscalaMaxima);
        }
    }
}

// Marca a rota como idempotente: o filtro e o parâmetro de header no OpenAPI.
internal sealed class IdempotenciaMetadata;

internal static class IdempotenciaEndpoints
{
    public static RouteHandlerBuilder Idempotente<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<IdempotencyFilter<T>>()
            .WithMetadata(new IdempotenciaMetadata())
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}

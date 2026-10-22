namespace Apolice.Api.Http;

internal static class RotasApi
{
    public const string GrupoVersionado = "/api/v{version:apiVersion}";
    public const string BaseV1 = "/api/v1";
    public const string Api = "/api";
    public const string Saude = "/health";
    public const string Scalar = "/scalar";
    public const string DocumentoOpenApi = "/openapi/{documentName}.json";
    public const string NomeDocumentoV1 = "v1";

    public static string Recurso(string colecao, Guid id) => $"{BaseV1}/{colecao}/{id}";
}

// Tags do OpenAPI (agrupamento no Scalar e no cliente gerado).
internal static class TagsApi
{
    public const string Autenticacao = "Autenticação";
    public const string Sistema = "Sistema";
    public const string Produtos = "Produtos";
    public const string Cotacoes = "Cotações";
    public const string Apolices = "Apólices";
    public const string Sinistros = "Sinistros";
    public const string Dashboard = "Dashboard";
    public const string Administracao = "Administração";
}

internal static class CabecalhosApi
{
    public const string CorrelationId = "X-Correlation-Id";
    public const string IdempotencyKey = "Idempotency-Key";
    public const string RequestedWith = "X-Requested-With";
    public const string ValorRequestedWith = "apolice-web";
    public const string RespostaRepetida = "Idempotent-Replayed";
    public const int TamanhoMaximoIdempotencyKey = 100;
}

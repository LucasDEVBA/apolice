using System.Diagnostics;
using Apolice.Domain.Comum;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Apolice.Api.Http;

// ProblemDetails (RFC 9457) com o contrato do SPEC 7: type = urn:apolice:problema:{codigo}, extensões codigo,
// traceId e correlationId. Toda resposta de erro (Result, validação, exceção, 401/403/404 do pipeline, 429) passa
// por Enriquecer via IProblemDetailsService, então o formato não depende de quem gerou o erro.
internal static class Problemas
{
    public const string PrefixoTipo = "urn:apolice:problema:";
    public const string ExtensaoCodigo = "codigo";
    public const string ExtensaoTraceId = "traceId";
    public const string ExtensaoCorrelationId = "correlationId";
    public const string CodigoValidacao = "Comum.Validacao";
    public const string CodigoErroInterno = "Comum.ErroInterno";
    public const string CodigoRequisicaoInvalida = "Comum.RequisicaoInvalida";
    public const string CodigoLimiteRequisicoes = "Comum.LimiteDeRequisicoes";

    public static int StatusDe(ErrorType tipo) => tipo switch
    {
        ErrorType.Validacao => StatusCodes.Status400BadRequest,
        ErrorType.NaoAutorizado => StatusCodes.Status401Unauthorized,
        ErrorType.Proibido => StatusCodes.Status403Forbidden,
        ErrorType.NaoEncontrado => StatusCodes.Status404NotFound,
        ErrorType.Conflito => StatusCodes.Status409Conflict,
        ErrorType.RegraDeNegocio => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static ProblemHttpResult De(Error erro)
    {
        ArgumentNullException.ThrowIfNull(erro);
        return TypedResults.Problem(Criar(StatusDe(erro.Tipo), erro.Codigo, erro.Mensagem));
    }

    public static ProblemDetails Criar(int status, string codigo, string? detalhe) =>
        new()
        {
            Status = status,
            Detail = detalhe,
            Extensions = { [ExtensaoCodigo] = codigo },
        };

    // Campos em camelCase, como no JSON ("segurado.cpf", "coberturas[1].lmi"): o formulário casa erro e campo direto.
    public static ProblemHttpResult Validacao(IEnumerable<ValidationFailure> falhas)
    {
        var erros = falhas
            .GroupBy(f => CaminhoCamelCase(f.PropertyName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var problema = new HttpValidationProblemDetails(erros)
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = "Um ou mais campos estão inválidos.",
            Extensions = { [ExtensaoCodigo] = CodigoValidacao },
        };
        return TypedResults.Problem(problema);
    }

    public static void Enriquecer(ProblemDetailsContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var problema = contexto.ProblemDetails;
        var http = contexto.HttpContext;
        var status = problema.Status ?? http.Response.StatusCode;
        var codigo = problema.Extensions.TryGetValue(ExtensaoCodigo, out var valor) && valor is string texto ? texto : CodigoPadrao(status);

        problema.Status = status;
        problema.Type = PrefixoTipo + codigo;
        problema.Title = Titulo(status);
        problema.Detail ??= DetalhePadrao(status);
        problema.Instance ??= http.Request.Path.Value;
        problema.Extensions[ExtensaoCodigo] = codigo;
        problema.Extensions[ExtensaoTraceId] = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier;
        problema.Extensions[ExtensaoCorrelationId] = CorrelacaoHttp.Obter(http);
    }

    private static string CaminhoCamelCase(string caminho) =>
        string.Join('.', caminho.Split('.').Select(parte => parte.Length == 0 ? parte : string.Concat(char.ToLowerInvariant(parte[0]).ToString(), parte[1..])));

    // Erros gerados pelo próprio pipeline (sem Result por trás) também ganham um código estável para o cliente.
    private static string CodigoPadrao(int status) => status switch
    {
        StatusCodes.Status400BadRequest => CodigoRequisicaoInvalida,
        StatusCodes.Status401Unauthorized => "Auth.NaoAutenticado",
        StatusCodes.Status403Forbidden => "Comum.Proibido",
        StatusCodes.Status404NotFound => "Comum.NaoEncontrado",
        StatusCodes.Status405MethodNotAllowed => "Comum.MetodoNaoPermitido",
        StatusCodes.Status409Conflict => "Comum.Conflito",
        StatusCodes.Status413PayloadTooLarge => "Comum.CorpoMuitoGrande",
        StatusCodes.Status415UnsupportedMediaType => "Comum.TipoDeConteudoNaoSuportado",
        StatusCodes.Status422UnprocessableEntity => "Comum.RegraDeNegocio",
        StatusCodes.Status429TooManyRequests => CodigoLimiteRequisicoes,
        StatusCodes.Status503ServiceUnavailable => "Comum.Indisponivel",
        >= StatusCodes.Status500InternalServerError => CodigoErroInterno,
        _ => $"Http.{status}",
    };

    private static string Titulo(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requisição inválida",
        StatusCodes.Status401Unauthorized => "Não autenticado",
        StatusCodes.Status403Forbidden => "Acesso negado",
        StatusCodes.Status404NotFound => "Não encontrado",
        StatusCodes.Status405MethodNotAllowed => "Método não permitido",
        StatusCodes.Status409Conflict => "Conflito",
        StatusCodes.Status413PayloadTooLarge => "Corpo muito grande",
        StatusCodes.Status415UnsupportedMediaType => "Tipo de conteúdo não suportado",
        StatusCodes.Status422UnprocessableEntity => "Operação não permitida",
        StatusCodes.Status429TooManyRequests => "Muitas requisições",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível",
        >= StatusCodes.Status500InternalServerError => "Erro interno",
        _ => "Erro na requisição",
    };

    private static string? DetalhePadrao(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Autentique-se para continuar.",
        StatusCodes.Status403Forbidden => "Seu perfil não tem permissão para esta operação.",
        StatusCodes.Status404NotFound => "O recurso solicitado não foi encontrado.",
        StatusCodes.Status405MethodNotAllowed => "Método HTTP não suportado nesta rota.",
        >= StatusCodes.Status500InternalServerError => "Ocorreu um erro inesperado. Informe o correlationId ao suporte.",
        _ => null,
    };
}

using Apolice.Application.Abstractions.Data;
using Microsoft.AspNetCore.Diagnostics;

namespace Apolice.Api.Http;

// Exceção = bug ou infraestrutura (falha previsível é Result). O cliente recebe só ProblemDetails com código,
// traceId e correlationId; stack trace e mensagem interna ficam no log.
internal sealed partial class ApoliceExceptionHandler(IProblemDetailsService problemas, ILogger<ApoliceExceptionHandler> logger)
    : IExceptionHandler
{
    private const string DetalheRequisicaoInvalida = "A requisição não pôde ser lida. Confira o formato do corpo e dos parâmetros.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        using var escopo = logger.BeginScope(new Dictionary<string, object?> { [CorrelacaoHttp.PropriedadeLog] = CorrelacaoHttp.Obter(httpContext) });

        // Cliente desconectou: não há para quem responder, e não é erro do servidor.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequisicaoCancelada(logger);
            return true;
        }

        var problema = Classificar(exception);
        Registrar(exception, problema.Status ?? StatusCodes.Status500InternalServerError);
        httpContext.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        return await problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problema });
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Classificar(Exception exception) => exception switch
    {
        ConflitoDeConcorrenciaException => Problemas.Criar(
            StatusCodes.Status409Conflict, ConcorrenciaErros.Conflito.Codigo, ConcorrenciaErros.Conflito.Mensagem),
        BadHttpRequestException requisicao => Problemas.Criar(requisicao.StatusCode, Problemas.CodigoRequisicaoInvalida, DetalheRequisicaoInvalida),
        _ => Problemas.Criar(StatusCodes.Status500InternalServerError, Problemas.CodigoErroInterno, null),
    };

    private void Registrar(Exception exception, int status)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogErroInesperado(logger, exception);
            return;
        }

        LogErroPrevisto(logger, status, exception.GetType().Name);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro inesperado ao processar a requisição.")]
    private static partial void LogErroInesperado(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requisição terminou com {Status} por {Excecao}.")]
    private static partial void LogErroPrevisto(ILogger logger, int status, string excecao);

    [LoggerMessage(Level = LogLevel.Information, Message = "Requisição cancelada pelo cliente.")]
    private static partial void LogRequisicaoCancelada(ILogger logger);
}

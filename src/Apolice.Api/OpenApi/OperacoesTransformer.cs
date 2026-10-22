using Apolice.Api.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Apolice.Api.OpenApi;

// Por operação: requisito Bearer só nas rotas protegidas (a política de fallback protege tudo que não é AllowAnonymous),
// os headers que filtros exigem (Idempotency-Key, X-Requested-With + cookie de sessão), query em camelCase e PDF binário.
internal sealed class OperacoesTransformer : IOpenApiOperationTransformer
{
    private const string TipoPdf = "application/pdf";

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadados = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadados.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(InformacoesDocumentoTransformer.EsquemaBearer, context.Document)] = [],
            });
        }

        if (metadados.OfType<IdempotenciaMetadata>().Any())
        {
            Adicionar(operation, Parametro(
                CabecalhosApi.IdempotencyKey,
                ParameterLocation.Header,
                obrigatorio: false,
                "Chave única por intenção (ex.: UUID). Repetir a mesma chave com o mesmo corpo devolve a resposta original.",
                CabecalhosApi.TamanhoMaximoIdempotencyKey));
        }

        if (metadados.OfType<CabecalhoCsrfMetadata>().Any())
        {
            Adicionar(operation, Parametro(CabecalhosApi.RequestedWith, ParameterLocation.Header, obrigatorio: true, $"Sempre \"{CabecalhosApi.ValorRequestedWith}\" (defesa contra CSRF).", null));
            Adicionar(operation, Parametro(CookieRefresh.Nome, ParameterLocation.Cookie, obrigatorio: false, "Refresh token gravado pelo login (HttpOnly).", null));
        }

        NomesDeQueryEmCamelCase(operation);
        DocumentarPdf(operation);
        return Task.CompletedTask;
    }

    private static OpenApiParameter Parametro(string nome, ParameterLocation local, bool obrigatorio, string descricao, int? tamanhoMaximo) =>
        new()
        {
            Name = nome,
            In = local,
            Required = obrigatorio,
            Description = descricao,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = tamanhoMaximo },
        };

    private static void Adicionar(OpenApiOperation operation, OpenApiParameter parametro)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(parametro);
    }

    // [AsParameters] expõe o nome da propriedade (Status, Limite); o binding é case-insensitive e o contrato é camelCase.
    private static void NomesDeQueryEmCamelCase(OpenApiOperation operation)
    {
        foreach (var parametro in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parametro.In == ParameterLocation.Query && parametro.Name is { Length: > 0 } nome)
            {
                parametro.Name = string.Concat(char.ToLowerInvariant(nome[0]).ToString(), nome[1..]);
            }
        }
    }

    private static void DocumentarPdf(OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return;
        }

        foreach (var resposta in operation.Responses.Values)
        {
            if (resposta.Content?.TryGetValue(TipoPdf, out var conteudo) == true)
            {
                conteudo.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
            }
        }
    }
}

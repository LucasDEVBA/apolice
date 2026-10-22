using Apolice.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Apolice.Api.OpenApi;

// Enums saem como texto (JsonStringEnumConverter): o "type": "string" explícito evita que o cliente gerado aceite
// qualquer valor. Os ProblemDetails ganham as extensões do contrato (codigo, traceId, correlationId).
internal sealed class EsquemasTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var tipo = context.JsonTypeInfo.Type;
        // Nunca "string | null" aqui: o componente do enum é compartilhado entre propriedades anuláveis e obrigatórias.
        if ((Nullable.GetUnderlyingType(tipo) ?? tipo).IsEnum && schema.Enum is { Count: > 0 })
        {
            schema.Type = JsonSchemaType.String;
        }

        if (typeof(ProblemDetails).IsAssignableFrom(tipo))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties[Problemas.ExtensaoCodigo] = Texto("Código estável do erro (ex.: Cotacao.Expirada); também no sufixo do type.");
            schema.Properties[Problemas.ExtensaoTraceId] = Texto("Trace do OpenTelemetry da requisição.");
            // Nulo só quando o erro acontece antes do middleware de correlação (ex.: cabeçalhos encaminhados inválidos).
            schema.Properties[Problemas.ExtensaoCorrelationId] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "Mesmo valor do header X-Correlation-Id.",
            };
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema Texto(string descricao) => new() { Type = JsonSchemaType.String, Description = descricao };
}

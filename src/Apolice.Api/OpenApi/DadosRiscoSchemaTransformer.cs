using System.Reflection;
using System.Text.Json.Serialization;
using Apolice.Domain.Tarifacao;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Apolice.Api.OpenApi;

// O gerador descreve [JsonPolymorphic] como anyOf; os dados de risco são exatamente UM dos três formatos, escolhido
// pelo discriminador "produto" (AUTO | RESID | VIDA). oneOf deixa isso explícito para o cliente gerado e o Scalar.
// O mapping é refeito a partir dos [JsonDerivedType] porque o gerador aponta para ids que não existem nos componentes
// (DadosRiscoDadosRiscoAuto, enquanto o esquema publicado é DadosRiscoAuto).
internal sealed class DadosRiscoSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string Discriminador = "produto";

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.JsonTypeInfo.Type != typeof(DadosRisco))
        {
            return Task.CompletedTask;
        }

        if (schema.AnyOf is { Count: > 0 } variantes)
        {
            schema.OneOf = variantes;
            schema.AnyOf = null;
        }

        schema.Discriminator = new OpenApiDiscriminator
        {
            PropertyName = Discriminador,
            Mapping = typeof(DadosRisco).GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                .ToDictionary(
                    derivado => Convert.ToString(derivado.TypeDiscriminator, System.Globalization.CultureInfo.InvariantCulture) ?? derivado.DerivedType.Name,
                    derivado => new OpenApiSchemaReference(derivado.DerivedType.Name, context.Document),
                    StringComparer.Ordinal),
        };
        return Task.CompletedTask;
    }
}

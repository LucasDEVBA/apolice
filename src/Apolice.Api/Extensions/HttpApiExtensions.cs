using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Apolice.Api.Http;
using Apolice.Application.Common;
using Asp.Versioning;

namespace Apolice.Api.Extensions;

internal static class HttpApiExtensions
{
    public static readonly ApiVersion VersaoV1 = new(1, 0);

    public static IServiceCollection AddHttpApi(this IServiceCollection services)
    {
        // Mesmo formato JSON da coluna DadosRisco e do outbox (camelCase, enums como texto, "produto" como discriminador).
        // NumberHandling estrito só na borda HTTP: os padrões Web aceitam "123" como número, e o contrato (e o cliente
        // gerado do OpenAPI) passaria a ser "number | string". A coluna e o outbox continuam com JsonOpcoes.Padrao.
        services.ConfigureHttpJsonOptions(opcoes =>
        {
            JsonOpcoes.Configurar(opcoes.SerializerOptions);
            opcoes.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            opcoes.SerializerOptions.TypeInfoResolver = (opcoes.SerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
                .WithAddedModifier(DiscriminadorObrigatorio.Aplicar);
        });
        services.AddSingleton<HashRequisicao>();
        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = Problemas.Enriquecer);
        services.AddExceptionHandler<ApoliceExceptionHandler>();
        services.AddLimitesRequisicao();
        services.AddApiVersioning(opcoes =>
            {
                opcoes.DefaultApiVersion = VersaoV1;
                opcoes.AssumeDefaultVersionWhenUnspecified = false;
                opcoes.ReportApiVersions = true;
                opcoes.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(opcoes =>
            {
                // "v1" = nome do documento OpenAPI; a rota sai com a versão substituída (/api/v1/...).
                opcoes.GroupNameFormat = "'v'VVV";
                opcoes.SubstituteApiVersionInUrl = true;
            });
        return services;
    }
}

// Sem o discriminador ("produto" nos dados de risco) o System.Text.Json tenta instanciar a base abstrata e lança
// NotSupportedException, que o binder das Minimal APIs não trata: virava 500 com log de erro para um corpo inválido do
// cliente. Como JsonException, o binder responde 400 (Comum.RequisicaoInvalida) como em qualquer JSON fora do contrato.
internal static class DiscriminadorObrigatorio
{
    public static void Aplicar(JsonTypeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Kind != JsonTypeInfoKind.Object || !info.Type.IsAbstract || info.CreateObject is not null
            || info.PolymorphismOptions is not { TypeDiscriminatorPropertyName: var campo })
        {
            return;
        }

        info.CreateObject = () => throw new JsonException($"O campo discriminador '{campo}' é obrigatório.");
    }
}

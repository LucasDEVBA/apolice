using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Tarifacao;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Apolice.Api.OpenApi;

// Ajustes sobre o documento já montado, onde os componentes são definitivos: um schema transformer vê cada esquema no
// contexto em que foi gerado (o mesmo enum aparece como propriedade anulável numa resposta e obrigatória num pedido) e
// nem sempre é chamado para os esquemas internos de variantes polimórficas. O cliente TypeScript é gerado daqui.
internal sealed class ContratoDocumentoTransformer : IOpenApiDocumentTransformer
{
    private const string Discriminador = "produto";
    private const string SufixoPedido = "Request";
    private const string EsquemaStreamOrfao = "Stream";
    private const string EsquemaLoginDemo = "LoginDemoRequest";
    private const string PropriedadePersona = "persona";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Components?.Schemas is not { } esquemas)
        {
            return Task.CompletedTask;
        }

        foreach (var (nome, esquema) in esquemas)
        {
            if (esquema is not OpenApiSchema concreto)
            {
                continue;
            }

            EnumComoTexto(concreto);
            if (nome.EndsWith(SufixoPedido, StringComparison.Ordinal))
            {
                OpcionaisForaDoRequired(concreto);
            }
        }

        DiscriminadorObrigatorioNasVariantes(esquemas);
        EnumDePersonas(esquemas);

        // O PDF é descrito como binário na própria operação (OperacoesTransformer); o componente ficaria sem referência.
        esquemas.Remove(EsquemaStreamOrfao);
        return Task.CompletedTask;
    }

    // Componente de enum é sempre texto e nunca contém null: a nulidade é da propriedade (oneOf [null, $ref]), senão o
    // tipo gerado do PEDIDO (CancelamentoRequest.motivo) passaria a aceitar null.
    private static void EnumComoTexto(OpenApiSchema esquema)
    {
        if (esquema.Enum is not { Count: > 0 } valores)
        {
            return;
        }

        esquema.Type = JsonSchemaType.String;
        esquema.Enum = [.. valores.Where(v => v is not null && !JsonNullSentinel.IsJsonNullSentinel(v) && v.GetValueKind() != JsonValueKind.Null)];
    }

    // Pedidos: propriedade anulável é opcional no contrato (lmi?, observacao?), e o servidor aceita a omissão. Nas
    // respostas fica required + nullable, porque a chave sempre é serializada (DefaultIgnoreCondition = Never).
    private static void OpcionaisForaDoRequired(OpenApiSchema esquema)
    {
        if (esquema.Required is not { Count: > 0 } obrigatorias || esquema.Properties is not { } propriedades)
        {
            return;
        }

        foreach (var (nome, propriedade) in propriedades)
        {
            if (Anulavel(propriedade))
            {
                obrigatorias.Remove(nome);
            }
        }
    }

    private static bool Anulavel(IOpenApiSchema propriedade) =>
        (propriedade.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Null))
        || propriedade.OneOf?.Any(o => o.Type == JsonSchemaType.Null) == true;

    // Sem "produto" o servidor responde 400: nas variantes ele é obrigatório. O pai fica só com oneOf + discriminator
    // (required/type ao lado do oneOf são resolvidos de formas diferentes pelos geradores).
    private static void DiscriminadorObrigatorioNasVariantes(IDictionary<string, IOpenApiSchema> esquemas)
    {
        foreach (var derivado in typeof(DadosRisco).GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false))
        {
            if (esquemas.TryGetValue(derivado.DerivedType.Name, out var variante) && variante is OpenApiSchema concreta)
            {
                concreta.Type = JsonSchemaType.Object;
                concreta.Required ??= new HashSet<string>(StringComparer.Ordinal);
                concreta.Required.Add(Discriminador);
            }
        }

        if (esquemas.TryGetValue(nameof(DadosRisco), out var pai) && pai is OpenApiSchema concretoPai)
        {
            concretoPai.Required = null;
            concretoPai.Type = null;
        }
    }

    private static void EnumDePersonas(IDictionary<string, IOpenApiSchema> esquemas)
    {
        if (esquemas.TryGetValue(EsquemaLoginDemo, out var login) && login is OpenApiSchema { Properties: { } propriedades }
            && propriedades.TryGetValue(PropriedadePersona, out var persona) && persona is OpenApiSchema concreta)
        {
            concreta.Enum = [.. new[] { PersonasDemo.Corretora, PersonasDemo.Corretor, PersonasDemo.Analista, PersonasDemo.Admin }
                .Select(p => (JsonNode)JsonValue.Create(p))];
        }
    }
}

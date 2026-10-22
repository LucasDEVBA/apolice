using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Apolice.Application.Common;

// Fonte única do formato JSON: API, coluna DadosRisco (text) e payload do outbox/inbox usam as mesmas opções.
// O polimorfismo de DadosRisco ("produto") vem dos atributos do próprio domínio.
public static class JsonOpcoes
{
    public static JsonSerializerOptions Padrao { get; } = Criar();

    // Para a API aplicar as mesmas regras nas opções que o ASP.NET já instancia (ConfigureHttpJsonOptions).
    public static void Configurar(JsonSerializerOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opcoes.PropertyNameCaseInsensitive = true;
        opcoes.DefaultIgnoreCondition = JsonIgnoreCondition.Never;

        // O frontend não garante que "produto" venha primeiro no objeto de dados de risco.
        opcoes.AllowOutOfOrderMetadataProperties = true;
        opcoes.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    }

    private static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        Configurar(opcoes);
        opcoes.MakeReadOnly();
        return opcoes;
    }
}

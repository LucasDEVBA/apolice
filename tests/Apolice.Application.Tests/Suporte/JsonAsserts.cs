using System.Text.Json;
using System.Text.Json.Nodes;

namespace Apolice.Application.Tests.Suporte;

internal static class JsonAsserts
{
    // Compara pelo valor: 3040.00 e 3040 são o mesmo número, e a escala do decimal não é contrato.
    public static void DeveSerJsonEquivalente(this string? atual, string esperado)
    {
        atual.ShouldNotBeNull();
        Equivalentes(JsonNode.Parse(atual), JsonNode.Parse(esperado)).ShouldBeTrue($"JSON diferente do esperado: {atual} ≠ {esperado}");
    }

    private static bool Equivalentes(JsonNode? a, JsonNode? b) => (a, b) switch
    {
        (null, null) => true,
        (JsonObject oa, JsonObject ob) => oa.Count == ob.Count && oa.All(p => ob.TryGetPropertyValue(p.Key, out var valor) && Equivalentes(p.Value, valor)),
        (JsonArray xa, JsonArray xb) => xa.Count == xb.Count && xa.Zip(xb).All(par => Equivalentes(par.First, par.Second)),
        (JsonValue va, JsonValue vb) when va.GetValueKind() == JsonValueKind.Number && vb.GetValueKind() == JsonValueKind.Number =>
            va.GetValue<decimal>() == vb.GetValue<decimal>(),
        (JsonValue va, JsonValue vb) => va.ToJsonString() == vb.ToJsonString(),
        _ => false,
    };
}

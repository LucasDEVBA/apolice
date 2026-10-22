using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apolice.Application.Common;

namespace Apolice.Api.IntegrationTests.Api.Suporte;

internal static class Respostas
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Na falha mostra o corpo: um ProblemDetails explica o porquê muito melhor do que só o status.
    public static async Task DeveTerStatusAsync(this HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        if (resposta.StatusCode != esperado)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(Ct);
            throw new ShouldAssertException($"Esperado {(int)esperado} {esperado}, veio {(int)resposta.StatusCode}: {corpo}");
        }
    }

    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        await resposta.DeveTerStatusAsync(esperado);
        return (await resposta.Content.ReadFromJsonAsync<T>(JsonOpcoes.Padrao, Ct))!;
    }

    // ProblemDetails do contrato (SPEC 7): sempre application/problem+json, type urn:apolice:problema:{codigo}, traceId
    // e correlationId (o mesmo do header de resposta).
    public static async Task<JsonElement> ProblemaAsync(this HttpResponseMessage resposta, HttpStatusCode esperado, string? codigo = null)
    {
        await resposta.DeveTerStatusAsync(esperado);
        resposta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problema.GetProperty("status").GetInt32().ShouldBe((int)esperado);
        var codigoRecebido = problema.GetProperty("codigo").GetString()!;
        problema.GetProperty("type").GetString().ShouldBe($"urn:apolice:problema:{codigoRecebido}");
        problema.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        problema.GetProperty("correlationId").GetString().ShouldBe(resposta.Headers.GetValues("X-Correlation-Id").Single());
        problema.TryGetProperty("stackTrace", out _).ShouldBeFalse();
        if (codigo is not null)
        {
            codigoRecebido.ShouldBe(codigo);
        }

        return problema;
    }
}

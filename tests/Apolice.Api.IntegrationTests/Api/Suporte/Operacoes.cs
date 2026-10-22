using System.Net;
using System.Net.Http.Json;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Sinistros;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Domain.Produtos;

namespace Apolice.Api.IntegrationTests.Api.Suporte;

// Passos de negócio pela API, para os testes chegarem ao estado que interessa sem repetir o caminho.
internal static class Operacoes
{
    public const int Parcelas = 4;
    public const decimal ValorReclamado = 6_500m;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<CotacaoDto> CriarCotacaoAsync(HttpClient cliente, CotacaoRequest? pedido = null)
    {
        using var resposta = await cliente.PostAsJsonAsync("/api/v1/cotacoes", pedido ?? Pedidos.CotacaoAuto(), JsonOpcoes.Padrao, Ct);
        return await resposta.LerAsync<CotacaoDto>(HttpStatusCode.Created);
    }

    public static async Task<CotacaoDto> CotacaoAceitaAsync(HttpClient cliente)
    {
        var cotacao = await CriarCotacaoAsync(cliente);
        using var resposta = await cliente.PostAsync($"/api/v1/cotacoes/{cotacao.Id}/aceitar", null, Ct);
        return await resposta.LerAsync<CotacaoDto>(HttpStatusCode.OK);
    }

    public static EmitirApoliceRequest Emissao(Guid cotacaoId) => new(cotacaoId, Pedidos.Hoje, Parcelas);

    public static async Task<HttpResponseMessage> EmitirAsync(HttpClient cliente, EmitirApoliceRequest pedido, string? chaveIdempotencia = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apolices") { Content = JsonContent.Create(pedido, options: JsonOpcoes.Padrao) };
        if (chaveIdempotencia is not null)
        {
            requisicao.Headers.TryAddWithoutValidation("Idempotency-Key", chaveIdempotencia).ShouldBeTrue();
        }

        return await cliente.SendAsync(requisicao, Ct);
    }

    public static async Task<ApoliceDto> ApoliceAsync(HttpClient cliente)
    {
        var cotacao = await CotacaoAceitaAsync(cliente);
        using var resposta = await EmitirAsync(cliente, Emissao(cotacao.Id));
        return await resposta.LerAsync<ApoliceDto>(HttpStatusCode.Created);
    }

    public static AbrirSinistroRequest Sinistro(Guid apoliceId, decimal valor = ValorReclamado) =>
        new(apoliceId, CodigosCobertura.Casco, Pedidos.Hoje, "Colisão traseira no semáforo com dano no para-choque e na tampa.", valor);

    public static async Task<HttpResponseMessage> AbrirSinistroAsync(HttpClient cliente, AbrirSinistroRequest pedido, string? chaveIdempotencia = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sinistros") { Content = JsonContent.Create(pedido, options: JsonOpcoes.Padrao) };
        if (chaveIdempotencia is not null)
        {
            requisicao.Headers.TryAddWithoutValidation("Idempotency-Key", chaveIdempotencia).ShouldBeTrue();
        }

        return await cliente.SendAsync(requisicao, Ct);
    }

    public static async Task<SinistroDto> SinistroAbertoAsync(HttpClient cliente, Guid apoliceId)
    {
        using var resposta = await AbrirSinistroAsync(cliente, Sinistro(apoliceId));
        return await resposta.LerAsync<SinistroDto>(HttpStatusCode.Created);
    }
}

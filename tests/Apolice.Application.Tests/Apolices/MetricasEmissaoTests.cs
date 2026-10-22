using System.Diagnostics.Metrics;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Observability;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Tests.Suporte;

namespace Apolice.Application.Tests.Apolices;

[Collection(nameof(TelemetriaGlobal))]
public sealed class MetricasEmissaoTests : TesteBase
{
    [Fact]
    public async Task Deve_contar_emissao_premio_e_sinistro_por_produto_quando_operacoes_concluem()
    {
        var medicoes = new List<(string Instrumento, double Valor, object? Produto)>();
        using var ouvinte = Ouvir(medicoes);

        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);

        medicoes.ShouldBe(
        [
            ("apolices_emitidas", 1d, "AUTO"),
            ("premio_emitido", 4_080.44d, "AUTO"),
            ("sinistros_abertos", 1d, "AUTO"),
        ]);
    }

    // Métrica de negócio só depois do commit: a emissão que perdeu a disputa de versão não conta.
    [Fact]
    public async Task Deve_nao_medir_nada_quando_a_gravacao_da_emissao_falha()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);
        var medicoes = new List<(string Instrumento, double Valor, object? Produto)>();
        using var ouvinte = Ouvir(medicoes);
        Amb.Banco.FalharProximoSaveComConflito = true;

        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() =>
            Amb.Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 1), Ct));

        medicoes.ShouldBeEmpty();
        Amb.Banco.Apolices.ShouldBeEmpty();
    }

    private static MeterListener Ouvir(List<(string Instrumento, double Valor, object? Produto)> medicoes)
    {
        var ouvinte = new MeterListener();
        ouvinte.InstrumentPublished = (instrumento, listener) =>
        {
            if (instrumento.Meter.Name == ApoliceTelemetry.Nome)
            {
                listener.EnableMeasurementEvents(instrumento);
            }
        };
        ouvinte.SetMeasurementEventCallback<long>((i, valor, tags, _) => medicoes.Add((i.Name, valor, ProdutoDe(tags))));
        ouvinte.SetMeasurementEventCallback<double>((i, valor, tags, _) => medicoes.Add((i.Name, valor, ProdutoDe(tags))));
        ouvinte.Start();
        return ouvinte;
    }

    private static object? ProdutoDe(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == ApoliceTelemetry.TagProduto)
            {
                return tag.Value;
            }
        }

        return null;
    }
}

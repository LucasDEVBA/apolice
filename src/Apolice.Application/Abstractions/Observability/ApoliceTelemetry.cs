using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Apolice.Application.Abstractions.Observability;

// Só BCL: a infraestrutura registra o ActivitySource e o Meter "Apolice" no OpenTelemetry.
public static class ApoliceTelemetry
{
    public const string Nome = "Apolice";
    public const string TagProduto = "produto";
    public const string TagMensagemId = "messaging.message.id";
    public const string TagTipoMensagem = "apolice.mensagem.tipo";
    public const string AtividadeEmitirApolice = "apolice.emitir";
    public const string AtividadeAbrirSinistro = "sinistro.abrir";

    // O tipo do evento entra no nome do span ("evento.consumir ApoliceEmitida"): cardinalidade baixa e fixa (um por evento).
    public const string AtividadeConsumirEvento = "evento.consumir";

    public static ActivitySource ActivitySource { get; } = new(Nome);

    public static Meter Meter { get; } = new(Nome);

    public static Counter<long> ApolicesEmitidas { get; } =
        Meter.CreateCounter<long>("apolices_emitidas", unit: "{apolice}", description: "Apólices emitidas.");

    // double (e não decimal): é o tipo que os exportadores OTLP de fato suportam para valores fracionários.
    public static Counter<double> PremioEmitido { get; } =
        Meter.CreateCounter<double>("premio_emitido", unit: "BRL", description: "Prêmio total emitido em reais.");

    public static Counter<long> SinistrosAbertos { get; } =
        Meter.CreateCounter<long>("sinistros_abertos", unit: "{sinistro}", description: "Sinistros abertos.");
}

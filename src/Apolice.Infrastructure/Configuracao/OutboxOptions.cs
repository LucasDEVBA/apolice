using System.ComponentModel.DataAnnotations;

namespace Apolice.Infrastructure.Configuracao;

public sealed class OutboxOptions
{
    public const string Secao = "Outbox";
    private const int IntervaloPadraoSegundos = 2;
    private const int LotePadrao = 50;
    private const int IntervaloMaximoSegundos = 3_600;
    private const int LoteMaximo = 1_000;

    // Padrão desligado: quem quer latência baixa (a API) liga; o relay cobre o resto de qualquer jeito.
    public bool DispatchImediato { get; set; }

    [Range(1, IntervaloMaximoSegundos)]
    public int IntervaloSegundos { get; set; } = IntervaloPadraoSegundos;

    [Range(1, LoteMaximo)]
    public int Lote { get; set; } = LotePadrao;
}

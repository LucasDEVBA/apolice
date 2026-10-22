namespace Apolice.Application.Features.Dashboard;

// Carteira = só o corretor logado; Global = Analista/Admin (carteira inteira).
public enum EscopoDashboard
{
    Carteira,
    Global,
}

// Frações (taxaConversao90d, sinistralidade12m) seguem a convenção do domínio: 0,25 = 25%.
public sealed record DashboardDto(
    EscopoDashboard Escopo,
    int ApolicesAtivas,
    decimal PremioEmitidoMes,
    IReadOnlyList<PremioMensalDto> PremioEmitido12Meses,
    int CotacoesAbertas,
    decimal TaxaConversao90d,
    int SinistrosEmAberto,
    decimal Sinistralidade12m,
    IReadOnlyList<ProdutoDashboardDto> PorProduto)
{
    public static DashboardDto De(EscopoDashboard escopo, MetricasDashboard metricas)
    {
        ArgumentNullException.ThrowIfNull(metricas);
        return new DashboardDto(
            escopo,
            metricas.ApolicesAtivas,
            metricas.PremioEmitidoMes,
            metricas.PremioEmitido12Meses,
            metricas.CotacoesAbertas,
            metricas.TaxaConversao90d,
            metricas.SinistrosEmAberto,
            metricas.Sinistralidade12m,
            metricas.PorProduto);
    }
}

// O que a leitura (IDashboardQueries) calcula; o escopo não vem daqui porque é decisão de autorização.
public sealed record MetricasDashboard(
    int ApolicesAtivas,
    decimal PremioEmitidoMes,
    IReadOnlyList<PremioMensalDto> PremioEmitido12Meses,
    int CotacoesAbertas,
    decimal TaxaConversao90d,
    int SinistrosEmAberto,
    decimal Sinistralidade12m,
    IReadOnlyList<ProdutoDashboardDto> PorProduto);

// Mes no formato "yyyy-MM" (ex.: "2026-10").
public sealed record PremioMensalDto(string Mes, decimal Valor);

public sealed record ProdutoDashboardDto(string ProdutoCodigo, string ProdutoNome, int ApolicesAtivas, decimal PremioEmitido);

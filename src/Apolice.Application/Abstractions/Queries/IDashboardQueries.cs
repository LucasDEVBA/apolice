using Apolice.Application.Features.Dashboard;

namespace Apolice.Application.Abstractions.Queries;

// Definições (todas no fuso de Brasília, filtradas por CorretorId quando preenchido):
// apolicesAtivas = status efetivo Ativa em Hoje; premioEmitidoMes = Σ PremioTotal emitido no mês de Hoje;
// premioEmitido12Meses = 12 meses até o mês de Hoje (inclusive), um item por mês, zero quando vazio;
// cotacoesAbertas = Calculada/Aceita com ValidaAte >= Agora; taxaConversao90d = emitidas ÷ criadas nos últimos
// 90 dias (0 se nenhuma criada); sinistrosEmAberto = Aberto/EmAnalise; sinistralidade12m = Σ indenizações
// (Aprovado/Pago) ÷ Σ prêmio emitido nos últimos 12 meses (0 se não houve prêmio).
// A porta devolve só as métricas: o escopo (Carteira/Global) é decisão de autorização, da aplicação.
public interface IDashboardQueries
{
    Task<MetricasDashboard> ObterAsync(FiltroDashboard filtro, CancellationToken cancellationToken);
}

public sealed record FiltroDashboard(Guid? CorretorId, DateOnly Hoje, DateTimeOffset Agora);

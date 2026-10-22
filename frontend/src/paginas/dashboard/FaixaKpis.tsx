import type { DashboardDto } from "@/api/contratos";
import { Kpi, KpiCard } from "@/ui/componentes/KpiCard";
import { Skeleton } from "@/ui/componentes/Skeleton";

/** Referência visual da sinistralidade (não vem da API: é a meta de exemplo do produto). */
const META_SINISTRALIDADE = 0.6;
const ROTULOS = [
  "Apólices ativas",
  "Prêmio do mês",
  "Cotações em aberto",
  "Conversão em 90 dias",
  "Sinistros em aberto",
  "Sinistralidade 12m",
] as const;

const fmtMes = new Intl.DateTimeFormat("pt-BR", { month: "long", timeZone: "America/Sao_Paulo" });

function KpisCarregando() {
  return (
    <KpiCard rotulo="Indicadores da carteira">
      {ROTULOS.map((rotulo) => (
        <li
          key={rotulo}
          className="relative min-w-0 border-r border-b border-line-soft bg-surface p-4 md:px-5 md:py-5 xl:border-0"
        >
          <span className="block text-label font-medium text-ink-2">{rotulo}</span>
          <Skeleton className="mt-3 h-8 w-24" />
          <Skeleton className="mt-3 h-2.5 w-28" />
        </li>
      ))}
    </KpiCard>
  );
}

/** Faixa de 6 KPIs só com campos do DashboardDto (DESIGN.md §4.4 e §10.2). */
export function FaixaKpis({ dados }: { dados: DashboardDto | undefined }) {
  if (!dados) return <KpisCarregando />;
  const mes = fmtMes.format(new Date());
  return (
    <KpiCard rotulo="Indicadores da carteira">
      <Kpi
        rotulo="Apólices ativas"
        valor={{ tipo: "inteiro", valor: dados.apolicesAtivas }}
        meta="em vigor hoje"
        para="/apolices?status=Ativa"
      />
      <Kpi
        rotulo={`Prêmio de ${mes}`}
        rotuloCurto="Prêmio do mês"
        valor={{ tipo: "moeda", valor: dados.premioEmitidoMes }}
        meta="emitido no mês"
        para="/apolices"
      />
      <Kpi
        rotulo="Cotações em aberto"
        valor={{ tipo: "inteiro", valor: dados.cotacoesAbertas }}
        meta="calculadas ou aceitas"
        para="/cotacoes?status=Calculada"
      />
      <Kpi
        rotulo="Conversão em 90 dias"
        rotuloCurto="Conversão 90 dias"
        valor={{ tipo: "percentual", valor: dados.taxaConversao90d }}
        meta="viraram apólice"
      />
      <Kpi
        rotulo="Sinistros em aberto"
        valor={{ tipo: "inteiro", valor: dados.sinistrosEmAberto }}
        meta="abertos ou em análise"
        para="/sinistros?status=Aberto"
      />
      <Kpi
        rotulo="Sinistralidade 12m"
        valor={{ tipo: "percentual", valor: dados.sinistralidade12m }}
        medidor={{ valor: dados.sinistralidade12m, meta: META_SINISTRALIDADE }}
      />
    </KpiCard>
  );
}

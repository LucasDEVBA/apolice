import type { DashboardDto } from "@/api/contratos";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { NumeroKpi } from "@/ui/componentes/KpiCard";
import { Skeleton } from "@/ui/componentes/Skeleton";
import { GraficoBarras } from "@/ui/graficos/GraficoBarras";
import { hojeEmBrasilia, mesDe } from "@/ui/utils/datas";
import { formatarCompacto, formatarMesPorExtenso } from "@/ui/utils/formatar";

const ALTURA_GRAFICO = 260;

/** "R$ 839", "R$ 3,5 mil": a unidade junto do número, para a média não ser lida como milhares. */
function moedaCompacta(valor: number): string {
  return `R$ ${formatarCompacto(Math.round(valor)).texto}`;
}

/** Prêmio emitido em 12 meses (DESIGN.md §4.15): mês corrente parcial em destaque e média. */
export function CartaoPremio({
  dados,
  className,
}: {
  dados: DashboardDto | undefined;
  className?: string;
}) {
  const meses = dados?.premioEmitido12Meses ?? [];
  const hoje = hojeEmBrasilia();
  const mesAtual = mesDe(hoje);
  const temMesAtual = meses.some((m) => m.mes === mesAtual);
  const fechados = meses.filter((m) => m.mes !== mesAtual);
  const total = meses.reduce((soma, m) => soma + m.valor, 0);
  const media =
    fechados.length > 0 ? fechados.reduce((s, m) => s + m.valor, 0) / fechados.length : undefined;
  const primeiro = meses[0]?.mes;
  const ultimo = meses.at(-1)?.mes;

  return (
    <Card className={className}>
      <CardHeader
        titulo="Prêmio emitido"
        subtitulo={
          temMesAtual ? "Últimos 12 meses, em R$ · mês atual parcial" : "Últimos 12 meses, em R$"
        }
        className="flex-nowrap"
        acoes={
          dados ? (
            <div className="text-right">
              <NumeroKpi valor={{ tipo: "moeda", valor: total }} tamanho="total" />
              <p className="mt-1 type-meta text-ink-3">no período</p>
            </div>
          ) : (
            <Skeleton className="h-7 w-24" />
          )
        }
      />
      <CardBody>
        {dados && primeiro && ultimo ? (
          <GraficoBarras
            dados={meses.map((m) => ({ mes: m.mes, valor: m.valor }))}
            rotulo={`Prêmio emitido por mês, de ${formatarMesPorExtenso(primeiro)} a ${formatarMesPorExtenso(ultimo)}`}
            {...(temMesAtual ? { mesCorrente: mesAtual, parcialAte: hoje } : {})}
            {...(media !== undefined ? { media } : {})}
            formatarMedia={moedaCompacta}
            altura={ALTURA_GRAFICO}
          />
        ) : (
          <div aria-busy className="flex h-[300px] items-end gap-3 pb-6">
            <span className="sr-only">Carregando gráfico…</span>
            {Array.from({ length: 12 }, (_, i) => (
              <Skeleton
                key={i}
                className="flex-1 rounded-b-none"
                style={{ height: `${String(30 + ((i * 37) % 50))}%` }}
              />
            ))}
          </div>
        )}
      </CardBody>
    </Card>
  );
}

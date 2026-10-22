import type { DashboardDto } from "@/api/contratos";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { NumeroKpi } from "@/ui/componentes/KpiCard";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { SkeletonTexto } from "@/ui/componentes/Skeleton";
import { BarraDistribuicao } from "@/ui/graficos/BarraDistribuicao";
import { formatarNumero, formatarPercentual } from "@/ui/utils/formatar";

/** Distribuição do prêmio por produto (porProduto do DashboardDto). */
export function CartaoDistribuicao({
  dados,
  className,
}: {
  dados: DashboardDto | undefined;
  className?: string;
}) {
  const produtos = dados?.porProduto ?? [];
  const total = produtos.reduce((s, p) => s + p.premioEmitido, 0);
  return (
    <Card className={className}>
      <CardHeader titulo="Distribuição por produto" subtitulo="Prêmio emitido e apólices ativas" />
      <CardBody>
        {dados ? (
          <>
            <BarraDistribuicao
              fatias={produtos.map((p) => ({ produto: p.produtoCodigo, valor: p.premioEmitido }))}
            />
            <ul className="mt-2 divide-y divide-line-soft">
              {produtos.map((p) => (
                <li
                  key={p.produtoCodigo}
                  className="flex items-center justify-between gap-3 py-3.5"
                >
                  <span>
                    <ProdutoTag produto={p.produtoCodigo} className="font-semibold" />
                    <span className="block pl-[18px] type-meta text-ink-3 tnum">
                      {formatarNumero(p.apolicesAtivas)}{" "}
                      {p.apolicesAtivas === 1 ? "apólice ativa" : "apólices ativas"}
                      {total > 0 && ` · ${formatarPercentual(p.premioEmitido / total, 0)}`}
                    </span>
                  </span>
                  <NumeroKpi valor={{ tipo: "moeda", valor: p.premioEmitido }} tamanho="sm" />
                </li>
              ))}
            </ul>
          </>
        ) : (
          <SkeletonTexto linhas={6} />
        )}
      </CardBody>
    </Card>
  );
}

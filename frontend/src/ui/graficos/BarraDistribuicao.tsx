import { PRODUTOS, type CodigoProduto } from "../componentes/produtos";
import { cn } from "../utils/cn";
import { formatarPercentual } from "../utils/formatar";

export interface FatiaDistribuicao {
  produto: CodigoProduto;
  valor: number;
}

/**
 * Barra empilhada de 12 px com vão de 2 px entre produtos (no escuro AUTO e RESID ficam a 2,6:1 um
 * do outro; o vão os separa). role="img" com a distribuição completa por extenso (DESIGN.md §4.15).
 */
export function BarraDistribuicao({
  fatias,
  className,
}: {
  fatias: readonly FatiaDistribuicao[];
  className?: string;
}) {
  const total = fatias.reduce((soma, f) => soma + f.valor, 0);
  const visiveis = fatias.filter((f) => f.valor > 0);
  const descricao =
    total === 0
      ? "Sem prêmio emitido no período"
      : visiveis
          .map((f) => `${PRODUTOS[f.produto].nome} ${formatarPercentual(f.valor / total, 0)}`)
          .join(", ");
  return (
    <div
      role="img"
      aria-label={`Distribuição por produto: ${descricao}`}
      className={cn("flex h-3 gap-0.5", className)}
    >
      {total === 0 ? (
        <span className="h-full w-full rounded-[4px] bg-meter-track" />
      ) : (
        visiveis.map((f) => (
          <span
            key={f.produto}
            className={cn(
              "h-full first:rounded-l-[4px] last:rounded-r-[4px]",
              PRODUTOS[f.produto].amostra,
            )}
            style={{ width: `${String((f.valor / total) * 100)}%` }}
          />
        ))
      )}
    </div>
  );
}

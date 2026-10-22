import type { ReactNode } from "react";

import { cn } from "@/ui/utils/cn";
import { formatarMoeda } from "@/ui/utils/formatar";

export interface LinhaCobertura {
  codigo: string;
  nome: string;
  lmi: number;
  premio: number;
  detalhe?: string;
}

export interface TotalCobertura {
  rotulo: string;
  valor: ReactNode;
  forte?: boolean;
}

/**
 * Coberturas abaixo de 640 px: cada item vira linha de 2 colunas (nome + código/LMI · prêmio),
 * em vez de a tabela rolar na horizontal e esconder os valores (DESIGN.md §5.3).
 */
export function ListaCoberturasCompacta({
  linhas,
  totais,
  rotulo,
}: {
  linhas: readonly LinhaCobertura[];
  totais: readonly TotalCobertura[];
  rotulo: string;
}) {
  return (
    <div className="sm:hidden">
      <ul aria-label={rotulo} className="divide-y divide-line-soft border-t border-line">
        {linhas.map((l) => (
          <li key={l.codigo} className="flex items-start justify-between gap-3 px-4 py-3">
            <span className="min-w-0">
              <span className="block text-sm text-ink">{l.nome}</span>
              <span className="block type-meta text-ink-3 tnum">
                <span className="mono-id">{l.codigo}</span>
                {l.lmi > 0 && ` · LMI ${formatarMoeda(l.lmi)}`}
                {l.detalhe && ` · ${l.detalhe}`}
              </span>
            </span>
            <span className="shrink-0 text-sm text-ink tnum">{formatarMoeda(l.premio)}</span>
          </li>
        ))}
      </ul>
      <dl className="border-t border-line px-4 pt-3 pb-4">
        {totais.map((t) => (
          <div
            key={t.rotulo}
            className={cn(
              "flex items-baseline justify-between gap-3 py-1 text-sm",
              t.forte ? "pt-2 font-semibold text-ink" : "text-ink-2",
            )}
          >
            <dt>{t.rotulo}</dt>
            <dd className="text-ink tnum">{t.valor}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

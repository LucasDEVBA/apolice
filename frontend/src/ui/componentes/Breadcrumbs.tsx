import { ChevronRight } from "lucide-react";
import { Fragment } from "react";
import { Link } from "react-router";

import { cn } from "../utils/cn";

export interface Migalha {
  rotulo: string;
  /** Sem href = página atual. */
  href?: string;
  /** Identificador (AUTO-2026-000042) em mono-id. */
  codigo?: boolean;
}

/** Trilha acima do H1 nas páginas de detalhe (DESIGN.md §4.3). */
export function Breadcrumbs({
  itens,
  className,
}: {
  itens: readonly Migalha[];
  className?: string;
}) {
  return (
    <nav aria-label="Trilha de navegação" className={className}>
      <ol className="flex flex-wrap items-center gap-1.5 text-label text-ink-3">
        {itens.map((item, indice) => (
          <Fragment key={`${item.rotulo}-${String(indice)}`}>
            {indice > 0 && (
              <li aria-hidden className="flex">
                <ChevronRight size={13} />
              </li>
            )}
            <li className={cn(item.codigo && "mono-id")}>
              {item.href ? (
                <Link
                  to={item.href}
                  className="rounded-xs hover:text-ink hover:underline hover:underline-offset-[3px]"
                >
                  {item.rotulo}
                </Link>
              ) : (
                <span aria-current="page" className="font-semibold text-ink-2">
                  {item.rotulo}
                </span>
              )}
            </li>
          </Fragment>
        ))}
      </ol>
    </nav>
  );
}

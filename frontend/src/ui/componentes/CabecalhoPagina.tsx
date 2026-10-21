import type { ReactNode } from "react";

import { cn } from "../utils/cn";
import { Breadcrumbs, type Migalha } from "./Breadcrumbs";

interface CabecalhoPaginaProps {
  titulo: ReactNode;
  subtitulo?: ReactNode;
  migalhas?: readonly Migalha[];
  /** Ações à direita (desktop) ou abaixo do título (mobile). */
  acoes?: ReactNode;
  className?: string;
}

/**
 * Cabeçalho de página: trilha → H1 em serifa → subtítulo. O H1 recebe tabIndex=-1 para o foco
 * pousar nele na troca de rota (o shell faz isso).
 */
export function CabecalhoPagina({
  titulo,
  subtitulo,
  migalhas,
  acoes,
  className,
}: CabecalhoPaginaProps) {
  return (
    <header
      className={cn(
        "flex flex-col gap-4 md:flex-row md:items-end md:justify-between md:gap-6",
        className,
      )}
    >
      <div className="min-w-0">
        {migalhas && migalhas.length > 0 && <Breadcrumbs itens={migalhas} className="mb-2.5" />}
        <h1 tabIndex={-1} className="type-title-page text-ink outline-none">
          {titulo}
        </h1>
        {subtitulo && <p className="mt-1.5 text-sm text-ink-3">{subtitulo}</p>}
      </div>
      {acoes && <div className="flex flex-wrap items-center gap-2.5 md:shrink-0">{acoes}</div>}
    </header>
  );
}

import { useRef, type ReactNode } from "react";

import { CabecalhoPagina } from "@/ui/componentes/CabecalhoPagina";
import type { Migalha } from "@/ui/componentes/Breadcrumbs";

import { useAnuncioDeRota } from "../anuncio-de-rota";

interface PaginaProps {
  titulo: string;
  subtitulo?: ReactNode;
  migalhas?: readonly Migalha[];
  acoes?: ReactNode;
  children?: ReactNode;
}

/** Moldura de página do shell: cabeçalho padrão + anúncio de rota (título e foco no H1). */
export function Pagina({ titulo, subtitulo, migalhas, acoes, children }: PaginaProps) {
  const ref = useRef<HTMLDivElement>(null);
  useAnuncioDeRota(titulo, ref);

  return (
    <div ref={ref} className="flex flex-col gap-4 md:gap-6">
      <CabecalhoPagina titulo={titulo} subtitulo={subtitulo} migalhas={migalhas} acoes={acoes} />
      {children}
    </div>
  );
}

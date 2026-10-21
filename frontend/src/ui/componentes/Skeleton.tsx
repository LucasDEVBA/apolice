import type { ComponentProps, ReactNode } from "react";

import { cn } from "../utils/cn";

/**
 * Bloco fantasma com as dimensões do conteúdo final (CLS zero). Só aparece após 200 ms, para
 * não piscar, e pulsa de opacidade sem shimmer; com movimento reduzido fica parado (DESIGN.md §4.19).
 */
export function Skeleton({ className, ...resto }: ComponentProps<"span">) {
  return (
    <span
      aria-hidden
      className={cn("block skeleton-bloco rounded-sm bg-skeleton", className)}
      {...resto}
    />
  );
}

/** Região carregando: aria-busy e "Carregando…" para o leitor de tela. */
export function RegiaoCarregando({
  carregando,
  children,
  rotulo = "Carregando…",
  className,
}: {
  carregando: boolean;
  children: ReactNode;
  rotulo?: string;
  className?: string;
}) {
  return (
    <div aria-busy={carregando || undefined} className={className}>
      {carregando && <span className="sr-only">{rotulo}</span>}
      {children}
    </div>
  );
}

/** Linhas de texto fantasma com larguras de 60–80%. */
export function SkeletonTexto({ linhas = 3, className }: { linhas?: number; className?: string }) {
  const larguras = ["w-4/5", "w-3/5", "w-[70%]"];
  return (
    <span className={cn("flex flex-col gap-2", className)} aria-hidden>
      {Array.from({ length: linhas }, (_, i) => (
        <Skeleton key={i} className={cn("h-2.5", larguras[i % larguras.length])} />
      ))}
    </span>
  );
}

import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { Gravura } from "../marca/Marca";
import { cn } from "../utils/cn";

interface EmptyStateProps {
  icone: LucideIcon;
  titulo: string;
  descricao?: ReactNode;
  /** Ação primária (Button) e link secundário. */
  acao?: ReactNode;
  acaoSecundaria?: ReactNode;
  nivel?: "h1" | "h2" | "h3";
  className?: string;
  children?: ReactNode;
}

/**
 * Estado vazio centralizado (DESIGN.md §4.16): chip de 48 px com anel de gravura a 0,18, título em
 * serifa, texto de apoio e ações. Variantes de uso: primeiro uso, sem resultado, sem permissão.
 */
export function EmptyState({
  icone: Icone,
  titulo,
  descricao,
  acao,
  acaoSecundaria,
  nivel = "h2",
  className,
  children,
}: EmptyStateProps) {
  const Titulo = nivel;
  return (
    <div
      className={cn(
        "mx-auto flex max-w-[420px] flex-col items-center px-4 py-10 text-center md:py-14",
        className,
      )}
    >
      <span className="relative grid size-24 place-items-center">
        <Gravura tamanho="anel" className="absolute inset-0 size-full" />
        <span className="relative grid size-12 place-items-center rounded-xl border border-line bg-surface-2 text-ink-2">
          <Icone size={22} aria-hidden />
        </span>
      </span>
      {/* Como H1 da tela (erro de rota), recebe o foco do anúncio de rota. */}
      <Titulo
        {...(nivel === "h1" ? { tabIndex: -1 } : {})}
        className="mt-3 font-serif text-xl leading-tight font-semibold tracking-[-0.01em] text-ink outline-none"
      >
        {titulo}
      </Titulo>
      {descricao && <div className="mt-2 text-sm leading-relaxed text-ink-2">{descricao}</div>}
      {children}
      {(acao ?? acaoSecundaria) && (
        <div className="mt-5 flex flex-wrap items-center justify-center gap-x-4 gap-y-2">
          {acao}
          {acaoSecundaria}
        </div>
      )}
    </div>
  );
}

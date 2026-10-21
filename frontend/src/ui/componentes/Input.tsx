import type { LucideIcon } from "lucide-react";
import type { ComponentProps, ReactNode } from "react";

import { cn } from "../utils/cn";

export type TamanhoCampo = "md" | "sm";

/** Caixa comum de Input, Select e Textarea: borda de controle 3:1, foco com anel, erro e desabilitado. */
export const CAIXA_DE_CAMPO =
  "rounded-md border border-control bg-surface text-ink transition-[border-color,box-shadow] duration-(--dur-fast) " +
  // Com erro, a borda continua vermelha também no foco (o anel de foco se soma a ela).
  "has-[[aria-invalid=true]]:border-bad has-[[aria-invalid=true]]:focus-within:border-bad " +
  "aria-invalid:border-bad aria-invalid:focus-visible:border-bad " +
  "has-disabled:border-control-disabled has-disabled:bg-surface-2 has-disabled:text-ink-3 " +
  "data-disabled:border-control-disabled data-disabled:bg-surface-2 data-disabled:text-ink-3";

const ALTURAS: Record<TamanhoCampo, string> = { md: "h-11", sm: "h-10" };

export interface InputProps extends Omit<ComponentProps<"input">, "size" | "prefix"> {
  tamanho?: TamanhoCampo;
  /** Prefixo interno em --ink-3 ("R$", "LMI R$"). */
  prefixo?: string;
  icone?: LucideIcon;
  /** Elemento à direita, dentro da caixa (ex.: botão de mostrar senha). */
  sufixo?: ReactNode;
  classeCaixa?: string;
}

/** Texto 16 px no mobile (o iOS não dá zoom no foco) e 15 px no desktop (DESIGN.md §4.8). */
export function Input({
  tamanho = "md",
  prefixo,
  icone: Icone,
  sufixo,
  className,
  classeCaixa,
  ...resto
}: InputProps) {
  return (
    <div
      className={cn(
        CAIXA_DE_CAMPO,
        "flex items-center gap-2.5 focus-campo px-3.5",
        ALTURAS[tamanho],
        classeCaixa,
      )}
    >
      {Icone && <Icone size={16} className="shrink-0 text-ink-3" aria-hidden />}
      {prefixo && (
        <span className="shrink-0 text-base text-ink-3 select-none md:text-campo" aria-hidden>
          {prefixo}
        </span>
      )}
      <input
        className={cn(
          "h-full min-w-0 flex-1 bg-transparent text-base text-ink outline-none placeholder:text-ink-3 md:text-campo",
          "tnum disabled:cursor-not-allowed",
          className,
        )}
        {...resto}
      />
      {sufixo}
    </div>
  );
}

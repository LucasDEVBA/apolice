import type { ComponentProps } from "react";

import { cn } from "../utils/cn";
import { formatarNumero } from "../utils/formatar";
import { CAIXA_DE_CAMPO } from "./Input";

/** Abaixo disto o leitor de tela passa a ouvir quantos caracteres restam. */
const AVISO_RESTANTES = 50;

export type TextareaProps = ComponentProps<"textarea">;

export function Textarea({ className, ...resto }: TextareaProps) {
  return (
    <textarea
      className={cn(
        CAIXA_DE_CAMPO,
        "min-h-28 resize-y focus-campo px-3.5 py-2.5 text-base outline-none placeholder:text-ink-3 md:text-campo",
        "disabled:cursor-not-allowed disabled:border-control-disabled disabled:bg-surface-2 disabled:text-ink-3",
        className,
      )}
      {...resto}
    />
  );
}

/**
 * "43 de 2.000" para o slot `contador` do FormField (mesma linha da ajuda, à direita). O limite
 * entra na descrição do campo; a contagem só é anunciada perto do fim, para não falar a cada tecla.
 */
export function ContadorCaracteres({ atual, maximo }: { atual: number; maximo: number }) {
  const restantes = Math.max(0, maximo - atual);
  const plural = restantes === 1 ? "caractere" : "caracteres";
  return (
    <>
      <span aria-hidden className="type-caption font-normal whitespace-nowrap text-ink-3 tnum">
        {formatarNumero(atual)} de {formatarNumero(maximo)}
      </span>
      <span className="sr-only">Máximo de {formatarNumero(maximo)} caracteres.</span>
      <span className="sr-only" aria-live="polite">
        {restantes <= AVISO_RESTANTES ? `Restam ${formatarNumero(restantes)} ${plural}.` : ""}
      </span>
    </>
  );
}

import type { LucideIcon } from "lucide-react";
import type { ComponentProps } from "react";

import { cn } from "../utils/cn";
import { Tooltip } from "./Tooltip";

type VarianteIconButton = "fantasma" | "secundario" | "marinho";

const VARIANTES: Record<VarianteIconButton, string> = {
  fantasma: "text-ink-2 hover:bg-hover-tint hover:text-ink",
  secundario:
    "border border-line bg-surface text-ink-2 shadow-xs hover:border-line-strong hover:bg-surface-2",
  // Foco dourado próprio: não depende de um ancestral .on-navy (DESIGN.md §7).
  marinho:
    "text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy focus-visible:outline-gold-line",
};

const TAMANHOS = { sm: "size-8", md: "size-10", lg: "size-11" } as const;

export interface IconButtonProps extends Omit<ComponentProps<"button">, "children" | "aria-label"> {
  icone: LucideIcon;
  /** Nome acessível obrigatório: o botão só tem ícone. */
  rotulo: string;
  variante?: VarianteIconButton;
  tamanho?: keyof typeof TAMANHOS;
  /** Mostra o rótulo em tooltip no hover/foco. */
  comTooltip?: boolean;
  /** Ponto de aviso no canto (ex.: notificações). O rótulo deve dizer quantas. */
  indicador?: boolean;
}

export function IconButton({
  icone: Icone,
  rotulo,
  variante = "fantasma",
  tamanho = "md",
  comTooltip = false,
  indicador = false,
  type = "button",
  className,
  ...resto
}: IconButtonProps) {
  const botao = (
    <button
      type={type}
      aria-label={rotulo}
      className={cn(
        "relative inline-grid shrink-0 place-items-center rounded-md transition-colors duration-(--dur-fast)",
        "disabled:cursor-not-allowed disabled:opacity-50",
        VARIANTES[variante],
        TAMANHOS[tamanho],
        className,
      )}
      {...resto}
    >
      <Icone size={18} aria-hidden />
      {indicador && (
        <span
          aria-hidden
          className="absolute top-2 right-2.5 size-2 rounded-full bg-bad ring-2 ring-surface"
        />
      )}
    </button>
  );
  return comTooltip ? <Tooltip conteudo={rotulo}>{botao}</Tooltip> : botao;
}

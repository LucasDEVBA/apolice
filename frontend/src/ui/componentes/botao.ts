import { cn } from "../utils/cn";

export type VarianteBotao =
  | "primario"
  | "secundario"
  | "fantasma"
  | "perigo"
  | "perigo-contorno"
  | "marinho-contorno"
  | "marinho-claro"
  | "marinho-perigo";

export type TamanhoBotao = "sm" | "md" | "lg";

const BASE =
  "relative inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-md font-semibold " +
  "transition-[background-color,border-color,color,box-shadow,transform] duration-(--dur-fast) ease-out " +
  "motion-safe:active:scale-[.98] aria-disabled:cursor-not-allowed aria-disabled:active:scale-100";

// Sobre marinho o azul de foco dá 2,1:1: as variantes marinho trazem o foco dourado (7,3:1) por
// conta própria, sem depender de um ancestral .on-navy (DESIGN.md §7).
const FOCO_MARINHO = "focus-visible:outline-gold-line";

const VARIANTES: Record<VarianteBotao, string> = {
  primario:
    "bg-primary text-primary-fg hover:bg-primary-hover active:bg-primary-active " +
    "aria-disabled:bg-surface-2 aria-disabled:text-ink-3 aria-disabled:ring-1 aria-disabled:ring-line aria-disabled:ring-inset",
  secundario:
    "border border-line bg-surface text-ink shadow-xs hover:border-line-strong hover:bg-surface-2 " +
    "aria-disabled:bg-surface-2 aria-disabled:text-ink-3 aria-disabled:hover:border-line",
  fantasma: "text-ink-2 hover:bg-hover-tint hover:text-ink aria-disabled:text-ink-3",
  perigo:
    "bg-danger text-danger-fg hover:bg-danger-hover " +
    "aria-disabled:bg-surface-2 aria-disabled:text-ink-3 aria-disabled:ring-1 aria-disabled:ring-line aria-disabled:ring-inset",
  "perigo-contorno":
    "border border-bad/45 text-bad hover:bg-bad-bg aria-disabled:border-line aria-disabled:text-ink-3",
  "marinho-contorno": cn(
    "border border-on-navy/22 bg-on-navy/2 text-on-navy hover:bg-on-navy/8 aria-disabled:text-on-navy-3",
    FOCO_MARINHO,
  ),
  "marinho-claro": cn(
    "bg-on-navy text-brand hover:bg-on-navy/90 aria-disabled:text-on-navy-3",
    FOCO_MARINHO,
  ),
  "marinho-perigo": cn(
    "border border-bad-on-navy/45 text-bad-on-navy hover:bg-bad-on-navy/10 aria-disabled:text-on-navy-3",
    FOCO_MARINHO,
  ),
};

const TAMANHOS: Record<TamanhoBotao, string> = {
  sm: "h-8 px-3 text-label",
  md: "h-10 px-4 text-sm",
  lg: "h-[46px] px-5 text-campo",
};

/** Classes do botão, para aplicar o visual a outro elemento (ex.: Link). */
export function buttonClasses({
  variante = "primario",
  tamanho = "md",
  larguraTotal = false,
}: { variante?: VarianteBotao; tamanho?: TamanhoBotao; larguraTotal?: boolean } = {}): string {
  return cn(BASE, VARIANTES[variante], TAMANHOS[tamanho], larguraTotal && "w-full");
}

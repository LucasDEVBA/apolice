import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

// Ensina ao tailwind-merge os utilitários próprios de tipografia, para "type-meta" não
// conflitar com "text-ink" (cor) nem ser descartado ao lado de outro "type-*".
const twMerge = extendTailwindMerge<"tipo-apolice">({
  extend: {
    // text-meta/label/item/campo são tamanhos de fonte (index.css), não cores.
    theme: { text: ["meta", "label", "item", "campo"] },
    classGroups: {
      "tipo-apolice": [
        {
          type: [
            "display-xl",
            "display-lg",
            "title-page",
            "title-auth",
            "title-modal",
            "title-card",
            "fig-hero",
            "fig-kpi",
            "fig-lg",
            "fig-total",
            "fig-sm",
            "label",
            "meta",
            "caption",
            "micro",
          ],
        },
      ],
    },
  },
});

/** Junta classes condicionais e resolve conflitos do Tailwind (a última vence). */
export function cn(...classes: ClassValue[]): string {
  return twMerge(clsx(classes));
}

import { cn } from "../utils/cn";
import { PRODUTOS, type CodigoProduto } from "./produtos";

export type { CodigoProduto } from "./produtos";

interface ProdutoTagProps {
  produto: CodigoProduto;
  /** "amostra": quadrado 10 px + nome (listas). "chip": ícone em chip de 36 px (feeds). */
  variante?: "amostra" | "chip";
  /** Mostra o código (AUTO) em mono ao lado do nome. */
  comCodigo?: boolean;
  className?: string;
}

/**
 * A cor do produto aparece só na amostra/chip; o texto é sempre --ink, para não confundir
 * produto com status (DESIGN.md §3.5).
 */
export function ProdutoTag({
  produto,
  variante = "amostra",
  comCodigo = false,
  className,
}: ProdutoTagProps) {
  const { nome, icone: Icone, amostra, chip } = PRODUTOS[produto];
  if (variante === "chip") {
    return (
      <span
        className={cn("inline-grid size-9 shrink-0 place-items-center rounded-lg", chip, className)}
        title={nome}
      >
        <Icone size={18} aria-hidden />
        <span className="sr-only">{nome}</span>
      </span>
    );
  }
  return (
    <span className={cn("inline-flex items-center gap-2 text-ink", className)}>
      <span aria-hidden className={cn("size-2.5 shrink-0 rounded-[3px]", amostra)} />
      <span>{nome}</span>
      {comCodigo && <span className="mono-id text-ink-3">{produto}</span>}
    </span>
  );
}

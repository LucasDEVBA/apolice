import { cn } from "../utils/cn";
import { iniciais } from "../utils/formatar";

const VARIANTES = {
  navy: "bg-avatar-navy text-avatar-navy-fg",
  steel: "bg-avatar-steel text-avatar-steel-fg",
  gold: "bg-avatar-gold text-avatar-gold-fg",
  // Selo escuro com fio dourado (persona Admin; usuário sobre marinho).
  marinho: "bg-brand text-gold-on-navy ring-1 ring-gold-line/45 ring-inset",
} as const;

const TAMANHOS = {
  sm: "size-8 text-xs",
  md: "size-9 text-sm",
  lg: "size-10 text-campo",
} as const;

interface AvatarProps {
  nome: string;
  variante?: keyof typeof VARIANTES;
  tamanho?: keyof typeof TAMANHOS;
  /** Anel dourado (usuário logado sobre marinho). */
  anel?: boolean;
  /**
   * O avatar costuma estar ao lado do nome escrito: por padrão é decorativo. Use true quando ele
   * for a única identificação da pessoa (ex.: coluna Responsável).
   */
  rotulado?: boolean;
  className?: string;
}

/** Iniciais em serifa 600 num círculo (DESIGN.md: avatares da referência). */
export function Avatar({
  nome,
  variante = "navy",
  tamanho = "md",
  anel = false,
  rotulado = false,
  className,
}: AvatarProps) {
  return (
    <span
      {...(rotulado ? { role: "img", "aria-label": nome, title: nome } : { "aria-hidden": true })}
      className={cn(
        "inline-grid shrink-0 place-items-center rounded-full font-serif font-semibold tracking-[0.01em]",
        VARIANTES[variante],
        TAMANHOS[tamanho],
        // Anel externo de 1 px no fio dourado (token), por fora do anel interno da variante.
        anel && "outline-1 outline-offset-0 outline-gold-line/55",
        className,
      )}
    >
      {iniciais(nome)}
    </span>
  );
}

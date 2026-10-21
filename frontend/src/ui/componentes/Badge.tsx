import { Circle, CircleCheck, CircleX, Info, TriangleAlert, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "../utils/cn";
import { apresentacaoDoStatus, type StatusBadgeProps, type Tom } from "./status";

export type { StatusApolice, StatusBadgeProps, StatusCotacao, StatusSinistro, Tom } from "./status";

const TOM_CLARO: Record<Tom, string> = {
  ok: "bg-ok-bg text-ok",
  warn: "bg-warn-bg text-warn",
  bad: "bg-bad-bg text-bad",
  info: "bg-info-bg text-info",
  neutral: "bg-neutral-bg text-neutral",
};

// Sobre marinho: fundo a ~15% da própria cor + anel interno de 1 px a 25% (DESIGN.md §3.4).
const TOM_MARINHO: Record<Tom, string> = {
  ok: "bg-ok-on-navy/15 text-ok-on-navy ring-1 ring-ok-on-navy/25 ring-inset",
  warn: "bg-warn-on-navy/15 text-warn-on-navy ring-1 ring-warn-on-navy/25 ring-inset",
  bad: "bg-bad-on-navy/15 text-bad-on-navy ring-1 ring-bad-on-navy/25 ring-inset",
  info: "bg-info-on-navy/15 text-info-on-navy ring-1 ring-info-on-navy/25 ring-inset",
  neutral: "bg-on-navy/10 text-on-navy-2 ring-1 ring-on-navy/20 ring-inset",
};

/** Ícone padrão de cada tom (para quando o badge usa ícone em vez de ponto). */
export const ICONE_DO_TOM: Record<Tom, LucideIcon> = {
  ok: CircleCheck,
  warn: TriangleAlert,
  bad: CircleX,
  info: Info,
  neutral: Circle,
};

export interface BadgeProps {
  tom: Tom;
  children: ReactNode;
  /** "ponto" (padrão), "plain" (sem marcador) ou um ícone de 13 px. */
  marcador?: "ponto" | "plain" | LucideIcon;
  sobreMarinho?: boolean;
  className?: string;
  title?: string;
}

/** Pílula de 24 px: ponto/ícone + rótulo. Estado nunca só por cor (DESIGN.md §4.6). */
export function Badge({
  tom,
  children,
  marcador = "ponto",
  sobreMarinho = false,
  className,
  title,
}: BadgeProps) {
  const Icone = typeof marcador === "string" ? null : marcador;
  return (
    <span
      title={title}
      className={cn(
        "inline-flex h-6 shrink-0 items-center gap-1.5 rounded-full px-2.5 text-meta leading-none font-semibold whitespace-nowrap tnum",
        sobreMarinho ? TOM_MARINHO[tom] : TOM_CLARO[tom],
        className,
      )}
    >
      {marcador === "ponto" && <span aria-hidden className="size-1.5 rounded-full bg-current" />}
      {Icone && <Icone size={13} strokeWidth={2} aria-hidden />}
      {children}
    </span>
  );
}

/** Badge de status de apólice, cotação ou sinistro, com o rótulo de negócio. */
export function StatusBadge(props: StatusBadgeProps) {
  const { tom, rotulo, icone } = apresentacaoDoStatus(props);
  return (
    <Badge
      tom={tom}
      marcador={icone ?? "ponto"}
      sobreMarinho={props.sobreMarinho ?? false}
      {...(props.className ? { className: props.className } : {})}
    >
      {rotulo}
    </Badge>
  );
}

import type { ComponentProps, ReactNode } from "react";

import { cn } from "../utils/cn";

type NivelTitulo = "h2" | "h3";

export function Card({ className, ...resto }: ComponentProps<"section">) {
  // Elevação vem da borda, nunca de sombra (DESIGN.md §3.11).
  return (
    <section
      className={cn("rounded-xl border border-line bg-surface text-ink", className)}
      {...resto}
    />
  );
}

interface CardHeaderProps {
  titulo: ReactNode;
  subtitulo?: ReactNode;
  /** Ações à direita: link "Ver histórico", total do gráfico, contador. */
  acoes?: ReactNode;
  nivel?: NivelTitulo;
  id?: string;
  /** tabIndex=-1 no título: recebe o foco por script (troca de etapa de um assistente). */
  focavel?: boolean;
  className?: string;
}

export function CardHeader({
  titulo,
  subtitulo,
  acoes,
  nivel = "h2",
  id,
  focavel = false,
  className,
}: CardHeaderProps) {
  const Titulo = nivel;
  return (
    <header
      className={cn(
        "flex flex-wrap items-start justify-between gap-x-4 gap-y-2 px-4 pt-[18px] md:px-6 md:pt-[22px]",
        className,
      )}
    >
      <div className="min-w-0">
        <Titulo
          id={id}
          {...(focavel ? { tabIndex: -1 } : {})}
          className="type-title-card text-ink outline-none"
        >
          {titulo}
        </Titulo>
        {subtitulo && <p className="mt-[3px] text-label text-ink-3">{subtitulo}</p>}
      </div>
      {/* ml-auto: se o cabeçalho quebrar, as ações seguem alinhadas à direita. */}
      {acoes && <div className="ml-auto flex shrink-0 items-center gap-2">{acoes}</div>}
    </header>
  );
}

export function CardBody({ className, ...resto }: ComponentProps<"div">) {
  return <div className={cn("p-4 md:p-6", className)} {...resto} />;
}

export function CardFooter({ className, ...resto }: ComponentProps<"footer">) {
  return (
    <footer
      className={cn(
        "flex flex-wrap items-center gap-2.5 rounded-b-xl border-t border-line bg-surface-2 px-4 py-4 md:px-6",
        className,
      )}
      {...resto}
    />
  );
}

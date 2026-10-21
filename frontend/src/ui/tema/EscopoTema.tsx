import { useState, type ReactNode } from "react";

import { cn } from "../utils/cn";
import { ContainerDePortal } from "./container-de-portal";

interface EscopoTemaProps {
  tema: "claro" | "escuro";
  children: ReactNode;
  className?: string;
}

/** Força um tema só nesta subárvore (usado na vitrine para mostrar os dois lado a lado). */
export function EscopoTema({ tema, children, className }: EscopoTemaProps) {
  const [container, setContainer] = useState<HTMLElement | null>(null);
  return (
    <div
      ref={setContainer}
      data-theme={tema === "escuro" ? "dark" : "light"}
      className={cn("bg-canvas text-ink", className)}
    >
      <ContainerDePortal value={container}>{children}</ContainerDePortal>
    </div>
  );
}

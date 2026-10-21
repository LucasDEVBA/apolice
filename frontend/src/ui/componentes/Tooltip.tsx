import { Tooltip as RadixTooltip } from "radix-ui";
import type { ReactElement, ReactNode } from "react";

import { useContainerDePortal } from "../tema/container-de-portal";

interface TooltipProps {
  conteudo: ReactNode;
  /** Elemento focável que dispara o tooltip (botão, link). */
  children: ReactElement;
  lado?: "top" | "right" | "bottom" | "left";
  atraso?: number;
}

/** Tooltip só em hover/foco, superfície marinho (DESIGN.md §4.15/§4.17). */
export function Tooltip({ conteudo, children, lado = "top", atraso }: TooltipProps) {
  const container = useContainerDePortal();
  return (
    <RadixTooltip.Root {...(atraso === undefined ? {} : { delayDuration: atraso })}>
      <RadixTooltip.Trigger asChild>{children}</RadixTooltip.Trigger>
      <RadixTooltip.Portal container={container}>
        <RadixTooltip.Content
          side={lado}
          sideOffset={6}
          collisionPadding={8}
          className="z-50 max-w-72 anima-popover rounded-md border border-brand-line bg-brand px-2.5 py-1.5 text-label leading-snug font-medium text-on-navy shadow-overlay"
        >
          {conteudo}
        </RadixTooltip.Content>
      </RadixTooltip.Portal>
    </RadixTooltip.Root>
  );
}

export const TooltipProvider = RadixTooltip.Provider;

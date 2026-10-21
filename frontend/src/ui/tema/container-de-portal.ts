import { createContext, use } from "react";

export const ContainerDePortal = createContext<HTMLElement | null>(null);

/**
 * Onde os portais do Radix (menus, diálogos, tooltips) devem renderizar. Dentro de um
 * EscopoTema, o portal fica no próprio escopo e herda o tema dele; fora, vai para o body.
 */
export function useContainerDePortal(): HTMLElement | undefined {
  return use(ContainerDePortal) ?? undefined;
}

import { render, type RenderResult } from "@testing-library/react";
import { LucideProvider } from "lucide-react";
import type { ReactElement, ReactNode } from "react";
import { createMemoryRouter, RouterProvider, type RouteObject } from "react-router";
import { configureAxe } from "vitest-axe";

import { TooltipProvider } from "@/ui/componentes/Tooltip";

/**
 * axe para componentes isolados: sem a regra "region" (o componente não está dentro de landmarks
 * aqui) e sem "color-contrast" (o jsdom não calcula layout/cor; o contraste está medido nos tokens).
 */
export const axe = configureAxe({
  rules: {
    region: { enabled: false },
    "color-contrast": { enabled: false },
  },
});

function Provedores({ children }: { children: ReactNode }) {
  return (
    <LucideProvider strokeWidth={1.6}>
      <TooltipProvider delayDuration={0}>{children}</TooltipProvider>
    </LucideProvider>
  );
}

/** Renderiza dentro de um router de memória (componentes com Link/NavLink). */
export function renderComRotas(
  elemento: ReactElement,
  { rota = "/", rotasExtras = [] }: { rota?: string; rotasExtras?: RouteObject[] } = {},
): RenderResult & { router: ReturnType<typeof createMemoryRouter> } {
  const router = createMemoryRouter(
    [{ path: "*", element: <Provedores>{elemento}</Provedores> }, ...rotasExtras],
    { initialEntries: [rota] },
  );
  return { ...render(<RouterProvider router={router} />), router };
}

export function renderSimples(elemento: ReactElement): RenderResult {
  return render(<Provedores>{elemento}</Provedores>);
}

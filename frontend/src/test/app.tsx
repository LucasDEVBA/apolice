import { QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { LucideProvider } from "lucide-react";
import { createMemoryRouter, RouterProvider } from "react-router";

import { criarQueryClient } from "@/app/cliente-query";
import type { IdPersona } from "@/app/personas";
import { rotas } from "@/app/rotas";
import { SessaoProvider } from "@/app/sessao/SessaoProvider";
import { db, definirSessaoRefresh, usuarioPorPersona } from "@/mocks/banco";
import { Toaster } from "@/ui/componentes/Toaster";
import { TooltipProvider } from "@/ui/componentes/Tooltip";

interface OpcoesApp {
  /** Persona já logada (o cookie de refresh do mock aponta para ela); null = anônimo. */
  persona?: IdPersona | null;
}

/**
 * O app inteiro (rotas reais, sessão, TanStack Query e toasts) contra o MSW. A sessão começa
 * como no navegador: o load faz POST /auth/refresh e o mock responde pelo "cookie" da persona.
 */
export function renderApp(rota: string, { persona = "corretora" }: OpcoesApp = {}) {
  definirSessaoRefresh(persona ? usuarioPorPersona(persona).id : null);
  const queryClient = criarQueryClient();
  queryClient.setDefaultOptions({
    queries: { ...queryClient.getDefaultOptions().queries, retry: false },
    mutations: { retry: false },
  });
  const router = createMemoryRouter(rotas, { initialEntries: [rota] });
  const resultado = render(
    <QueryClientProvider client={queryClient}>
      <LucideProvider strokeWidth={1.6}>
        <TooltipProvider delayDuration={0}>
          <SessaoProvider>
            <RouterProvider router={router} />
            <Toaster />
          </SessaoProvider>
        </TooltipProvider>
      </LucideProvider>
    </QueryClientProvider>,
  );
  return { ...resultado, router, queryClient, banco: db() };
}

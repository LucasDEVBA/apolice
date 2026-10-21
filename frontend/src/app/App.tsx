import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { LucideProvider } from "lucide-react";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";

import { Toaster } from "@/ui/componentes/Toaster";
import { TooltipProvider } from "@/ui/componentes/Tooltip";

import { PersonaProvider } from "./PersonaContexto";
import { rotas } from "./rotas";

const SEGUNDOS_DADO_FRESCO = 30;
const ATRASO_TOOLTIP_MS = 300;

// Modo data (createBrowserRouter): errorElement por rota, rotas como objetos e, na fase 2,
// loaders que pré-aquecem o cache do TanStack Query antes de a tela montar.
const router = createBrowserRouter(rotas);

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: SEGUNDOS_DADO_FRESCO * 1000, retry: 1, refetchOnWindowFocus: false },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <LucideProvider strokeWidth={1.6}>
        <TooltipProvider delayDuration={ATRASO_TOOLTIP_MS}>
          <PersonaProvider>
            <RouterProvider router={router} />
            <Toaster />
          </PersonaProvider>
        </TooltipProvider>
      </LucideProvider>
    </QueryClientProvider>
  );
}

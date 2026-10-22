import { QueryClientProvider, type QueryClient } from "@tanstack/react-query";
import { LucideProvider } from "lucide-react";
import { useState } from "react";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";

import { Toaster } from "@/ui/componentes/Toaster";
import { TooltipProvider } from "@/ui/componentes/Tooltip";

import { criarQueryClient } from "./cliente-query";
import { rotas } from "./rotas";
import { SessaoProvider } from "./sessao/SessaoProvider";

const ATRASO_TOOLTIP_MS = 300;

// Modo data (createBrowserRouter): errorElement por rota e rotas como objetos.
const router = createBrowserRouter(rotas);

export function App({ queryClient }: { queryClient?: QueryClient }) {
  const [cliente] = useState(() => queryClient ?? criarQueryClient());
  return (
    <QueryClientProvider client={cliente}>
      <LucideProvider strokeWidth={1.6}>
        <TooltipProvider delayDuration={ATRASO_TOOLTIP_MS}>
          <SessaoProvider>
            <RouterProvider router={router} />
            <Toaster />
          </SessaoProvider>
        </TooltipProvider>
      </LucideProvider>
    </QueryClientProvider>
  );
}

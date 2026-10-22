/**
 * Mock da API no navegador (VITE_API_MOCK=1), só para desenvolvimento e screenshots. main.tsx
 * importa este módulo atrás de import.meta.env.DEV: ele nunca entra no build de produção.
 */
import { setupWorker } from "msw/browser";

import { resetarBanco, restaurarSessaoDev } from "./banco";
import { handlers } from "./handlers";

const LATENCIA_DEV_MS = 180;

export async function iniciarMockDaApi(): Promise<void> {
  resetarBanco({ latenciaMs: LATENCIA_DEV_MS, atrasoWorkerMs: 1_500 });
  restaurarSessaoDev();
  const worker = setupWorker(...handlers);
  await worker.start({
    onUnhandledRequest: "bypass",
    quiet: true,
    serviceWorker: { url: "/mockServiceWorker.js" },
  });
  console.info({
    nivel: "info",
    acao: "mock_api_ativo",
    detalhe: "Respostas da API vêm do MSW (dados fictícios).",
  });
}

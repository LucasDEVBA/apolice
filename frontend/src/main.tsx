import "./ui/estilos/index.css";

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./app/App";
import { prepararAnunciador } from "./ui/utils/anunciar";

const raiz = document.getElementById("root");
if (!raiz) throw new Error("Elemento #root não encontrado em index.html.");

/**
 * VITE_API_MOCK=1 liga o MSW só em desenvolvimento (screenshots, demo sem backend). O ramo inteiro
 * some do build de produção, porque import.meta.env.DEV é falso lá.
 */
async function prepararMock(): Promise<void> {
  if (!import.meta.env.DEV || import.meta.env.VITE_API_MOCK !== "1") return;
  const { iniciarMockDaApi } = await import("./mocks/browser");
  await iniciarMockDaApi();
}

function montar(elemento: HTMLElement) {
  prepararAnunciador();
  createRoot(elemento).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}

prepararMock()
  .catch((erro: unknown) => {
    console.error({ nivel: "erro", acao: "mock_api_falhou", erro });
  })
  .finally(() => montar(raiz));

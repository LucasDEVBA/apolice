import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { fileURLToPath, URL } from "node:url";

import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, type Plugin, type ProxyOptions } from "vite";

// 127.0.0.1, não "localhost": o compose publica só em IPv4, e localhost pode resolver para ::1.
const API_LOCAL = "http://127.0.0.1:8080";

// Mesma origem para o browser (como na Vercel): o cookie de refresh é first-party e não há CORS.
const proxyDaApi: Record<string, ProxyOptions> = Object.fromEntries(
  ["/api", "/scalar", "/openapi", "/health"].map((prefixo) => [
    prefixo,
    { target: API_LOCAL, changeOrigin: false },
  ]),
);

// Bibliotecas em chunks próprios: mudam pouco, então ficam no cache do navegador entre deploys
// do app (e nenhum chunk passa do limite de 500 kB).
const GRUPOS_DE_VENDOR = [
  { name: "react", test: /[\\/]node_modules[\\/](react|react-dom|scheduler)[\\/]/, priority: 30 },
  {
    name: "roteamento-dados",
    test: /[\\/]node_modules[\\/](react-router|@tanstack)[\\/]/,
    priority: 20,
  },
  {
    name: "ui-base",
    test: /[\\/]node_modules[\\/](radix-ui|@radix-ui|@floating-ui|react-remove-scroll[^/\\]*|aria-hidden|use-sidecar|use-callback-ref|sonner|lucide-react|tailwind-merge|clsx)[\\/]/,
    priority: 10,
  },
];

/**
 * Service worker do MSW servido só pelo dev server (VITE_API_MOCK=1): fora de public/, para o
 * arquivo nunca ir para o build de produção.
 */
function workerDoMockEmDev(): Plugin {
  const exigir = createRequire(import.meta.url);
  return {
    name: "apolice:msw-worker-dev",
    apply: "serve",
    configureServer(server) {
      server.middlewares.use("/mockServiceWorker.js", (_requisicao, resposta) => {
        resposta.setHeader("Content-Type", "text/javascript; charset=utf-8");
        resposta.end(readFileSync(exigir.resolve("msw/mockServiceWorker.js")));
      });
    },
  };
}

export default defineConfig({
  plugins: [react(), tailwindcss(), workerDoMockEmDev()],
  build: {
    rolldownOptions: { output: { codeSplitting: { groups: GRUPOS_DE_VENDOR } } },
  },
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  server: { port: 5173, strictPort: true, proxy: proxyDaApi },
  preview: { port: 4173, strictPort: true, proxy: proxyDaApi },
});

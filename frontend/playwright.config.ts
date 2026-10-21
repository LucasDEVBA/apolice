import { defineConfig, devices } from "@playwright/test";

const BASE_URL = process.env.BASE_URL ?? "http://localhost:4173";
const NO_CI = Boolean(process.env.CI);
// Permite rodar localmente com o Chrome instalado (PW_CHANNEL=chrome) sem baixar o Chromium do Playwright.
const canal = process.env.PW_CHANNEL;

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: NO_CI,
  retries: NO_CI ? 2 : 0,
  reporter: NO_CI ? [["github"], ["html", { open: "never" }]] : [["list"]],
  use: {
    baseURL: BASE_URL,
    locale: "pt-BR",
    timezoneId: "America/Sao_Paulo",
    trace: "retain-on-failure",
    ...(canal ? { channel: canal } : {}),
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } },
    },
    // 375 px é a largura-alvo do mobile (DESIGN.md §7). Chromium com toque, não WebKit: o CI só
    // instala o Chromium.
    {
      name: "mobile",
      use: { ...devices["Pixel 7"], viewport: { width: 375, height: 812 } },
    },
  ],
  // Sobe o build de produção sozinho quando ninguém subiu. reuseExistingServer sempre: o CI já
  // serve o preview (com proxy para a API do compose) antes desta etapa.
  webServer: {
    command: "pnpm run build && pnpm exec vite preview --port 4173 --strictPort",
    url: BASE_URL,
    reuseExistingServer: true,
    timeout: 180_000,
  },
});

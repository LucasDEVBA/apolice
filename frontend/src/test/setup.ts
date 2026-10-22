import "@testing-library/jest-dom/vitest";

import { cleanup, configure } from "@testing-library/react";
import { afterAll, afterEach, beforeAll, beforeEach, expect, vi } from "vitest";
import * as matchersAxe from "vitest-axe/matchers";

import { definirTokenDeAcesso } from "@/api/http";
import { resetarBanco } from "@/mocks/banco";
import { servidorMock } from "@/mocks/node";

import { matchMediaFalso, restaurarMediaQueries } from "./media-query";

expect.extend(matchersAxe);

// Telas carregam por React.lazy (o 1º import de uma rota transforma o chunk no Vitest) e a sessão
// é restaurada por refresh antes de qualquer conteúdo: 1 s padrão é pouco para a primeira tela.
configure({ asyncUtilTimeout: 10_000 });

// MSW: toda chamada à API nos testes passa pelos handlers; chamada sem handler é erro de teste.
beforeAll(() => servidorMock.listen({ onUnhandledRequest: "error" }));
beforeEach(() => {
  resetarBanco({ atrasoWorkerMs: 50, latenciaMs: 0 });
});
afterAll(() => servidorMock.close());

afterEach(() => {
  cleanup();
  servidorMock.resetHandlers();
  definirTokenDeAcesso(null);
  window.sessionStorage.clear();
  document.documentElement.removeAttribute("data-theme");
  document.documentElement.removeAttribute("data-density");
  window.localStorage.clear();
  restaurarMediaQueries();
});

// --- APIs de navegador que o jsdom não implementa (Radix, Recharts, media queries) ---

// O BroadcastChannel do Node cruza os workers do Vitest: a "renovação de outra aba" viria de
// outro arquivo de teste. Sem ele, cada arquivo é uma aba isolada (o http.ts tolera a ausência).
Object.defineProperty(globalThis, "BroadcastChannel", { configurable: true, value: undefined });

// Configurável por teste (simularDesktop, simularMediaQueries): sem isso os ramos de desktop e
// de movimento reduzido nunca rodariam.
Object.defineProperty(window, "matchMedia", { writable: true, value: matchMediaFalso });

class ResizeObserverFalso {
  observe(): void {
    // jsdom não tem layout: nada a observar.
  }
  unobserve(): void {
    // idem
  }
  disconnect(): void {
    // idem
  }
}
globalThis.ResizeObserver = ResizeObserverFalso;

// O axe sonda canvas para medir ícones/fontes; sem o pacote "canvas" o jsdom só avisaria no console.
HTMLCanvasElement.prototype.getContext = vi.fn(() => null);
Element.prototype.scrollIntoView = vi.fn();
Element.prototype.hasPointerCapture = vi.fn(() => false);
Element.prototype.releasePointerCapture = vi.fn();
Element.prototype.setPointerCapture = vi.fn();

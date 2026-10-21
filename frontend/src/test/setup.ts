import "@testing-library/jest-dom/vitest";

import { cleanup } from "@testing-library/react";
import { afterEach, expect, vi } from "vitest";
import * as matchersAxe from "vitest-axe/matchers";

import { matchMediaFalso, restaurarMediaQueries } from "./media-query";

expect.extend(matchersAxe);

afterEach(() => {
  cleanup();
  document.documentElement.removeAttribute("data-theme");
  document.documentElement.removeAttribute("data-density");
  window.localStorage.clear();
  restaurarMediaQueries();
});

// --- APIs de navegador que o jsdom não implementa (Radix, Recharts, media queries) ---

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

import { useSyncExternalStore } from "react";

const CONSULTA = "(prefers-reduced-motion: reduce)";

function assinar(aoMudar: () => void): () => void {
  if (typeof window.matchMedia !== "function") return () => undefined;
  const consulta = window.matchMedia(CONSULTA);
  consulta.addEventListener("change", aoMudar);
  return () => consulta.removeEventListener("change", aoMudar);
}

function ler(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia(CONSULTA).matches;
}

/** Alimenta Recharts e animações programáticas (DESIGN.md §6). */
export function useReducedMotion(): boolean {
  return useSyncExternalStore(assinar, ler, () => false);
}

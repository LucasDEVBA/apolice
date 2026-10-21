/**
 * Preferências do usuário guardadas no navegador (tema e densidade). O script
 * public/preferencias-iniciais.js aplica as mesmas chaves antes do React montar.
 */
import { useSyncExternalStore } from "react";

export type PreferenciaTema = "sistema" | "claro" | "escuro";
export type TemaResolvido = "claro" | "escuro";
export type Densidade = "confortavel" | "compacta";

const CHAVE_TEMA = "apolice.tema";
const CHAVE_DENSIDADE = "apolice.densidade";
const CONSULTA_ESCURO = "(prefers-color-scheme: dark)";
const EVENTO = "apolice:preferencias";

const TEMAS = new Set<PreferenciaTema>(["sistema", "claro", "escuro"]);
const DENSIDADES = new Set<Densidade>(["confortavel", "compacta"]);

function ler(chave: string): string | null {
  try {
    return window.localStorage.getItem(chave);
  } catch {
    // Storage bloqueado (modo privado, política do navegador): vale o padrão.
    return null;
  }
}

function gravar(chave: string, valor: string): void {
  try {
    window.localStorage.setItem(chave, valor);
  } catch {
    // Sem storage a preferência vale só nesta sessão; a UI continua funcionando.
  }
}

function ehTema(valor: string | null): valor is PreferenciaTema {
  return valor !== null && TEMAS.has(valor as PreferenciaTema);
}

function ehDensidade(valor: string | null): valor is Densidade {
  return valor !== null && DENSIDADES.has(valor as Densidade);
}

export function lerPreferenciaTema(): PreferenciaTema {
  const valor = ler(CHAVE_TEMA);
  return ehTema(valor) ? valor : "sistema";
}

export function lerDensidade(): Densidade {
  const valor = ler(CHAVE_DENSIDADE);
  return ehDensidade(valor) ? valor : "confortavel";
}

function sistemaEscuro(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia(CONSULTA_ESCURO).matches;
}

export function resolverTema(preferencia: PreferenciaTema): TemaResolvido {
  if (preferencia === "sistema") return sistemaEscuro() ? "escuro" : "claro";
  return preferencia;
}

function aplicarNoDocumento(): void {
  const raiz = document.documentElement;
  const tema = lerPreferenciaTema();
  if (tema === "sistema") raiz.removeAttribute("data-theme");
  else raiz.setAttribute("data-theme", tema === "escuro" ? "dark" : "light");
  raiz.setAttribute("data-density", lerDensidade() === "compacta" ? "compact" : "comfortable");
}

export function definirPreferenciaTema(tema: PreferenciaTema): void {
  gravar(CHAVE_TEMA, tema);
  aplicarNoDocumento();
  window.dispatchEvent(new Event(EVENTO));
}

export function definirDensidade(densidade: Densidade): void {
  gravar(CHAVE_DENSIDADE, densidade);
  aplicarNoDocumento();
  window.dispatchEvent(new Event(EVENTO));
}

function assinar(aoMudar: () => void): () => void {
  const consulta =
    typeof window.matchMedia === "function" ? window.matchMedia(CONSULTA_ESCURO) : null;
  window.addEventListener(EVENTO, aoMudar);
  window.addEventListener("storage", aoMudar);
  consulta?.addEventListener("change", aoMudar);
  return () => {
    window.removeEventListener(EVENTO, aoMudar);
    window.removeEventListener("storage", aoMudar);
    consulta?.removeEventListener("change", aoMudar);
  };
}

export interface EstadoTema {
  preferencia: PreferenciaTema;
  resolvido: TemaResolvido;
}

// Snapshot em string para o useSyncExternalStore comparar por valor (objeto novo a cada leitura
// causaria renderização infinita).
function snapshotTema(): string {
  const preferencia = lerPreferenciaTema();
  return `${preferencia}|${resolverTema(preferencia)}`;
}

export function useTema(): EstadoTema {
  const snapshot = useSyncExternalStore(assinar, snapshotTema, () => "sistema|claro");
  const [preferencia, resolvido] = snapshot.split("|") as [PreferenciaTema, TemaResolvido];
  return { preferencia, resolvido };
}

export function useDensidade(): Densidade {
  return useSyncExternalStore(assinar, lerDensidade, () => "confortavel");
}

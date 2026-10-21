/**
 * Regiões aria-live globais. Ficam no body desde o início (uma região criada junto com o texto
 * não é anunciada por vários leitores de tela), uma polite e outra assertive.
 */
type Prioridade = "polite" | "assertive";

const regioes = new Map<Prioridade, HTMLElement>();
const ATRASO_MS = 60;

function regiao(prioridade: Prioridade): HTMLElement {
  const existente = regioes.get(prioridade);
  if (existente?.isConnected) return existente;
  const el = document.createElement("div");
  el.setAttribute("aria-live", prioridade);
  el.setAttribute("aria-atomic", "true");
  el.setAttribute("role", prioridade === "assertive" ? "alert" : "status");
  el.dataset.anunciador = prioridade;
  el.className = "sr-only";
  document.body.append(el);
  regioes.set(prioridade, el);
  return el;
}

/** Cria as regiões cedo (chamado no boot do app). */
export function prepararAnunciador(): void {
  regiao("polite");
  regiao("assertive");
}

/** Anuncia um texto. Limpa antes e escreve depois de um instante para repetir o mesmo texto. */
export function anunciar(texto: string, prioridade: Prioridade = "polite"): void {
  const el = regiao(prioridade);
  el.textContent = "";
  window.setTimeout(() => {
    el.textContent = texto;
  }, ATRASO_MS);
}

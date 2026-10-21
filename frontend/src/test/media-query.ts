import { vi } from "vitest";

type Casa = (consulta: string) => boolean;

const NENHUMA: Casa = () => false;
let casa: Casa = NENHUMA;

/**
 * Define, por teste, quais media queries casam. O padrão (nenhuma) é o mobile sem preferências;
 * o setup volta ao padrão depois de cada teste.
 */
export function simularMediaQueries(predicado: Casa): void {
  casa = predicado;
}

/** Desktop (largura ≥ 768 px): casa as consultas "(width >= …)". */
export function simularDesktop(): void {
  simularMediaQueries((consulta) => consulta.includes("width >="));
}

export function restaurarMediaQueries(): void {
  casa = NENHUMA;
}

export const matchMediaFalso = vi.fn((consulta: string) => ({
  matches: casa(consulta),
  media: consulta,
  onchange: null,
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  addListener: vi.fn(),
  removeListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

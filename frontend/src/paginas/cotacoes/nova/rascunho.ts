import type { FormularioCotacao } from "./modelo";

const PREFIXO = "apolice.rascunho-cotacao.v1";

export interface Rascunho {
  valores: FormularioCotacao;
  etapaMaxima: number;
  salvoEm: string;
}

interface RascunhoGuardado extends Rascunho {
  usuarioId: string;
}

/**
 * Uma chave por usuário: o rascunho tem nome, CPF e e-mail de um segurado de UMA carteira, e o
 * sessionStorage da aba sobrevive à troca de persona (LGPD, SPEC §9).
 */
function chave(usuarioId: string): string {
  return `${PREFIXO}:${usuarioId}`;
}

/** Rascunho no sessionStorage (some ao fechar a aba): pode faltar, estar bloqueado ou corrompido. */
export function lerRascunho(usuarioId: string): Rascunho | null {
  try {
    const texto = sessionStorage.getItem(chave(usuarioId));
    if (!texto) return null;
    const {
      usuarioId: dono,
      valores,
      etapaMaxima,
      salvoEm,
    } = JSON.parse(texto) as Partial<RascunhoGuardado>;
    // Rascunho de outro usuário (ou sem dono, de versão antiga) nunca abre para este.
    if (dono !== usuarioId || !valores) return null;
    if (typeof etapaMaxima !== "number" || typeof salvoEm !== "string") return null;
    return { valores, etapaMaxima, salvoEm };
  } catch {
    return null;
  }
}

export function salvarRascunho(usuarioId: string, rascunho: Rascunho): boolean {
  try {
    const guardado: RascunhoGuardado = { ...rascunho, usuarioId };
    sessionStorage.setItem(chave(usuarioId), JSON.stringify(guardado));
    return true;
  } catch {
    // Sem storage (modo privado, cota): o assistente segue funcionando, só não guarda o rascunho.
    return false;
  }
}

export function descartarRascunho(usuarioId: string): void {
  try {
    sessionStorage.removeItem(chave(usuarioId));
  } catch {
    // Nada a remover se o storage está bloqueado.
  }
}

/** Fim de sessão (sair, trocar de persona, expirar, outro usuário): nenhum rascunho fica na aba. */
export function descartarTodosOsRascunhos(): void {
  try {
    const chaves = Array.from({ length: sessionStorage.length }, (_, i) => sessionStorage.key(i));
    for (const k of chaves) {
      if (k === PREFIXO || k?.startsWith(`${PREFIXO}:`)) sessionStorage.removeItem(k);
    }
  } catch {
    // Storage bloqueado: também não havia rascunho guardado.
  }
}

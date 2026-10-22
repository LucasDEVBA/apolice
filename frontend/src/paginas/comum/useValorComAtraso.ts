import { useEffect, useState } from "react";

/** Valor com debounce: só muda depois de `atrasoMs` sem alterações (simulação ao vivo, rascunho). */
export function useValorComAtraso<T>(valor: T, atrasoMs: number): T {
  const [atrasado, setAtrasado] = useState(valor);
  useEffect(() => {
    const timer = window.setTimeout(() => setAtrasado(valor), atrasoMs);
    return () => window.clearTimeout(timer);
  }, [valor, atrasoMs]);
  return atrasado;
}

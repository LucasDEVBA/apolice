import type { Query } from "@tanstack/react-query";
import { useRef } from "react";

/** Intervalo e janela máxima do polling dos efeitos assíncronos do worker (boas-vindas, triagem). */
export const INTERVALO_POLLING_MS = 2_000;
export const JANELA_POLLING_MS = 30_000;

/**
 * refetchInterval que repete enquanto `aguardando(dado)` for verdadeiro e para quando o dado chega
 * ou depois de 30 s (o worker pode estar parado; a tela não fica consultando para sempre).
 */
export function useIntervaloAteChegar<T>(
  aguardando: (dado: T) => boolean,
): (query: Query<T, Error, T>) => number | false {
  const inicio = useRef<number | null>(null);
  return (query) => {
    const dado = query.state.data;
    if (dado === undefined || !aguardando(dado)) {
      inicio.current = null;
      return false;
    }
    inicio.current ??= Date.now();
    return Date.now() - inicio.current < JANELA_POLLING_MS ? INTERVALO_POLLING_MS : false;
  };
}

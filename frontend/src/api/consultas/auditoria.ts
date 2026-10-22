import { useInfiniteQuery } from "@tanstack/react-query";

import { listarAuditoria } from "../endpoints/auditoria";
import { chaves, TAMANHO_PAGINA } from "./chaves";

export function useAuditoria() {
  return useInfiniteQuery({
    queryKey: chaves.auditoria.lista(),
    queryFn: ({ pageParam, signal }) =>
      listarAuditoria({ cursor: pageParam, limite: TAMANHO_PAGINA }, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (ultima) => ultima.proximoCursor,
  });
}

import {
  keepPreviousData,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";

import type { CotacaoDto, CotacaoRequest } from "../contratos";
import {
  aceitarCotacao,
  criarCotacao,
  listarCotacoes,
  obterCotacao,
  recusarCotacao,
  simularCotacao,
} from "../endpoints/cotacoes";
import { chaves, TAMANHO_PAGINA, type FiltrosCotacoes } from "./chaves";

const MINUTO = 60_000;

export function useCotacoes(filtros: FiltrosCotacoes, limite = TAMANHO_PAGINA) {
  return useInfiniteQuery({
    queryKey: [...chaves.cotacoes.lista(filtros), limite],
    queryFn: ({ pageParam, signal }) =>
      listarCotacoes({ ...filtros, cursor: pageParam, limite }, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (ultima) => ultima.proximoCursor,
  });
}

export function useCotacao(id: string) {
  return useQuery({
    queryKey: chaves.cotacoes.detalhe(id),
    queryFn: ({ signal }) => obterCotacao(id, signal),
  });
}

/**
 * Simulação ao vivo (POST /cotacoes/simular). O pedido chega já com debounce de 300 ms; a chave é o
 * próprio pedido, então voltar a um cenário já calculado não chama a API de novo. Enquanto recalcula,
 * o último resultado válido continua na tela (placeholderData).
 */
export function useSimulacaoCotacao(pedido: CotacaoRequest | null) {
  return useQuery({
    queryKey: pedido ? chaves.cotacoes.simulacao(pedido) : [...chaves.cotacoes.todas, "simulacao"],
    queryFn: ({ signal }) => {
      if (!pedido) throw new Error("Simulação sem pedido.");
      return simularCotacao(pedido, signal);
    },
    enabled: pedido !== null,
    placeholderData: keepPreviousData,
    staleTime: 5 * MINUTO,
    retry: false,
  });
}

function invalidarResumo(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: chaves.cotacoes.listas() }),
    queryClient.invalidateQueries({ queryKey: chaves.dashboard }),
  ]);
}

export function useCriarCotacao() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: criarCotacao,
    meta: { errosNoFormulario: true },
    onSuccess: async (cotacao) => {
      queryClient.setQueryData(chaves.cotacoes.detalhe(cotacao.id), cotacao);
      await invalidarResumo(queryClient);
    },
  });
}

function useTransicaoCotacao(transicao: (id: string) => Promise<CotacaoDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: transicao,
    onSuccess: async (cotacao) => {
      queryClient.setQueryData(chaves.cotacoes.detalhe(cotacao.id), cotacao);
      await invalidarResumo(queryClient);
    },
  });
}

export const useAceitarCotacao = () => useTransicaoCotacao(aceitarCotacao);
export const useRecusarCotacao = () => useTransicaoCotacao(recusarCotacao);

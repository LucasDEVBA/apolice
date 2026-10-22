import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";

import type { AbrirSinistroRequest, AprovarSinistroRequest, SinistroDto } from "../contratos";
import {
  abrirSinistro,
  aprovarSinistro,
  iniciarAnaliseSinistro,
  listarSinistros,
  negarSinistro,
  obterSinistro,
  registrarPagamentoSinistro,
} from "../endpoints/sinistros";
import { chaves, TAMANHO_PAGINA, type FiltrosSinistros } from "./chaves";
import { useIntervaloAteChegar } from "./polling";

export function useSinistros(filtros: FiltrosSinistros) {
  return useInfiniteQuery({
    queryKey: chaves.sinistros.lista(filtros),
    queryFn: ({ pageParam, signal }) =>
      listarSinistros({ ...filtros, cursor: pageParam, limite: TAMANHO_PAGINA }, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (ultima) => ultima.proximoCursor,
  });
}

/** Com a triagem automática ligada, o sinistro recém-aberto espera o score do worker. */
export function useSinistro(id: string, triagemAutomatica: boolean) {
  const intervalo = useIntervaloAteChegar<SinistroDto>(
    (sinistro) => triagemAutomatica && sinistro.triagem === null,
  );
  return useQuery({
    queryKey: chaves.sinistros.detalhe(id),
    queryFn: ({ signal }) => obterSinistro(id, signal),
    refetchInterval: intervalo,
  });
}

function aoMudarSinistro(queryClient: QueryClient, sinistro: SinistroDto) {
  queryClient.setQueryData(chaves.sinistros.detalhe(sinistro.id), sinistro);
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: chaves.sinistros.listas() }),
    // A apólice lista os sinistros e as notificações deles.
    queryClient.invalidateQueries({ queryKey: chaves.apolices.detalhe(sinistro.apoliceId) }),
    queryClient.invalidateQueries({ queryKey: chaves.dashboard }),
  ]);
}

interface VariaveisAbertura {
  pedido: AbrirSinistroRequest;
  chaveIdempotencia: string;
}

export function useAbrirSinistro() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ pedido, chaveIdempotencia }: VariaveisAbertura) =>
      abrirSinistro(pedido, chaveIdempotencia),
    meta: { errosNoFormulario: true },
    onSuccess: (sinistro) => aoMudarSinistro(queryClient, sinistro),
  });
}

function useAcaoSinistro<V>(acao: (variaveis: V) => Promise<SinistroDto>, noFormulario = false) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: acao,
    meta: { errosNoFormulario: noFormulario },
    onSuccess: (sinistro) => aoMudarSinistro(queryClient, sinistro),
  });
}

export const useIniciarAnalise = () => useAcaoSinistro((id: string) => iniciarAnaliseSinistro(id));
export const useAprovarSinistro = () =>
  useAcaoSinistro(
    ({ id, pedido }: { id: string; pedido: AprovarSinistroRequest }) => aprovarSinistro(id, pedido),
    true,
  );
export const useNegarSinistro = () =>
  useAcaoSinistro(
    ({ id, motivo }: { id: string; motivo: string }) => negarSinistro(id, { motivo }),
    true,
  );
export const useRegistrarPagamento = () =>
  useAcaoSinistro((id: string) => registrarPagamentoSinistro(id));

import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";

import type {
  ApoliceDto,
  EmitirApoliceRequest,
  EndossoRequest,
  MotivoCancelamento,
} from "../contratos";
import {
  baixarDocumentoApolice,
  cancelarApolice,
  emitirApolice,
  endossarApolice,
  listarApolices,
  obterApolice,
  simularCancelamento,
  simularEndosso,
} from "../endpoints/apolices";
import { chaves, TAMANHO_PAGINA, type FiltrosApolices } from "./chaves";
import { useIntervaloAteChegar } from "./polling";

/** Só a apólice emitida há pouco espera o worker; uma antiga sem boas-vindas não fica em polling. */
const MINUTOS_RECEM_EMITIDA = 10;

export function useApolices(filtros: FiltrosApolices, limite = TAMANHO_PAGINA) {
  return useInfiniteQuery({
    queryKey: [...chaves.apolices.lista(filtros), limite],
    queryFn: ({ pageParam, signal }) =>
      listarApolices({ ...filtros, cursor: pageParam, limite }, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (ultima) => ultima.proximoCursor,
  });
}

/** O worker ainda não mandou as boas-vindas (efeito assíncrono do evento ApoliceEmitida). */
export function aguardandoBoasVindas(apolice: ApoliceDto, agora = Date.now()): boolean {
  const recente = agora - new Date(apolice.emitidaEm).getTime() < MINUTOS_RECEM_EMITIDA * 60_000;
  const semBoasVindas =
    apolice.boasVindasEnviadasEm === null ||
    !apolice.notificacoes.some((n) => n.tipo === "BoasVindas");
  return recente && semBoasVindas;
}

export function useApolice(id: string) {
  const intervalo = useIntervaloAteChegar<ApoliceDto>((apolice) => aguardandoBoasVindas(apolice));
  return useQuery({
    queryKey: chaves.apolices.detalhe(id),
    queryFn: ({ signal }) => obterApolice(id, signal),
    refetchInterval: intervalo,
  });
}

function invalidarAposMudancaNaApolice(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: chaves.apolices.listas() }),
    queryClient.invalidateQueries({ queryKey: chaves.dashboard }),
  ]);
}

interface VariaveisEmissao {
  pedido: EmitirApoliceRequest;
  chaveIdempotencia: string;
}

export function useEmitirApolice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ pedido, chaveIdempotencia }: VariaveisEmissao) =>
      emitirApolice(pedido, chaveIdempotencia),
    meta: { errosNoFormulario: true },
    onSuccess: async (apolice) => {
      queryClient.setQueryData(chaves.apolices.detalhe(apolice.id), apolice);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: chaves.cotacoes.detalhe(apolice.cotacaoId) }),
        queryClient.invalidateQueries({ queryKey: chaves.cotacoes.listas() }),
        invalidarAposMudancaNaApolice(queryClient),
      ]);
    },
  });
}

export function useSimulacaoEndosso(id: string, pedido: EndossoRequest | null) {
  return useQuery({
    queryKey: pedido
      ? chaves.apolices.simulacaoEndosso(id, pedido)
      : [...chaves.apolices.todas, "simulacao-endosso", id],
    queryFn: ({ signal }) => {
      if (!pedido) throw new Error("Simulação sem pedido.");
      return simularEndosso(id, pedido, signal);
    },
    enabled: pedido !== null,
    staleTime: 0,
    retry: false,
  });
}

export function useEndossarApolice(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (pedido: EndossoRequest) => endossarApolice(id, pedido),
    meta: { errosNoFormulario: true },
    onSuccess: async (apolice) => {
      queryClient.setQueryData(chaves.apolices.detalhe(apolice.id), apolice);
      await invalidarAposMudancaNaApolice(queryClient);
    },
  });
}

export function useSimulacaoCancelamento(id: string, motivo: MotivoCancelamento | null) {
  return useQuery({
    queryKey: motivo
      ? chaves.apolices.simulacaoCancelamento(id, motivo)
      : [...chaves.apolices.todas, "simulacao-cancelamento", id],
    queryFn: ({ signal }) => {
      if (!motivo) throw new Error("Simulação sem motivo.");
      return simularCancelamento(id, { motivo }, signal);
    },
    enabled: motivo !== null,
    staleTime: 0,
    retry: false,
  });
}

export function useCancelarApolice(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (motivo: MotivoCancelamento) => cancelarApolice(id, { motivo }),
    onSuccess: async (apolice) => {
      queryClient.setQueryData(chaves.apolices.detalhe(apolice.id), apolice);
      await invalidarAposMudancaNaApolice(queryClient);
    },
  });
}

/** Baixa o PDF como blob com o token (não dá para usar <a href>: o token não vai no link). */
export function useBaixarDocumento() {
  return useMutation({
    mutationFn: async ({ id, numero }: { id: string; numero: string }) => {
      const pdf = await baixarDocumentoApolice(id);
      salvarArquivo(pdf, `${numero}.pdf`);
    },
  });
}

function salvarArquivo(conteudo: Blob, nome: string) {
  const url = URL.createObjectURL(conteudo);
  const link = document.createElement("a");
  link.href = url;
  link.download = nome;
  link.rel = "noopener";
  document.body.append(link);
  link.click();
  link.remove();
  // O navegador já copiou a referência ao iniciar o download.
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

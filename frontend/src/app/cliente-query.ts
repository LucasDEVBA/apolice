import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";

import { notificar } from "@/ui/componentes/notificar";

import { ErroApi, ehErroTransitorio, mensagemDoErro } from "@/api/erros";

declare module "@tanstack/react-query" {
  interface Register {
    mutationMeta: {
      /** Não mostra toast (simulações ao vivo; quem chama trata). */
      silenciosa?: boolean;
      /** Erros por campo vão para o formulário; o toast só aparece se não houver campo. */
      errosNoFormulario?: boolean;
    };
  }
}

const SEGUNDOS_DADO_FRESCO = 30;
const MAXIMO_TENTATIVAS = 2;
const ATRASO_BASE_MS = 500;
const ATRASO_MAXIMO_MS = 4_000;

/** Retry só para o que pode passar sozinho: rede, 5xx e 429. 4xx é definitivo. */
export function deveRepetir(falhas: number, erro: unknown): boolean {
  return falhas < MAXIMO_TENTATIVAS && ehErroTransitorio(erro);
}

function atrasoExponencial(tentativa: number): number {
  return Math.min(ATRASO_BASE_MS * 2 ** tentativa, ATRASO_MAXIMO_MS);
}

/** Erros de mutação viram toast com a mensagem da API e o correlationId copiável. */
function notificarErroDeMutacao(
  erro: unknown,
  meta: { silenciosa?: boolean; errosNoFormulario?: boolean } | undefined,
) {
  if (meta?.silenciosa) return;
  if (meta?.errosNoFormulario && erro instanceof ErroApi && erro.temErrosPorCampo) return;
  // 401 sem sessão já tem o próprio aviso ("Sua sessão expirou").
  if (erro instanceof ErroApi && erro.naoAutorizado) return;
  const { titulo, descricao } = mensagemDoErro(erro);
  notificar.erro({
    titulo,
    ...(descricao ? { descricao } : {}),
    ...(erro instanceof ErroApi && erro.correlationId ? { correlationId: erro.correlationId } : {}),
  });
}

export function criarQueryClient(): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (erro, query) => {
        console.warn({ nivel: "aviso", acao: "consulta_falhou", chave: query.queryKey[0], erro });
      },
    }),
    mutationCache: new MutationCache({
      onError: (erro, _variaveis, _contexto, mutacao) => notificarErroDeMutacao(erro, mutacao.meta),
    }),
    defaultOptions: {
      queries: {
        staleTime: SEGUNDOS_DADO_FRESCO * 1000,
        retry: deveRepetir,
        retryDelay: atrasoExponencial,
        refetchOnWindowFocus: false,
      },
      mutations: { retry: false },
    },
  });
}

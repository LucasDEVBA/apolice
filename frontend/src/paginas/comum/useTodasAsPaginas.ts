import { useEffect } from "react";

interface ConsultaPaginada {
  data?: { pages: readonly unknown[] } | undefined;
  isPending: boolean;
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  isFetchNextPageError: boolean;
  fetchNextPage: () => Promise<unknown>;
}

/** Teto de páginas de 100 (até 1.000 registros) para uma fila que a API não filtra. */
export const TETO_PAGINAS_FILA = 10;

export interface ProgressoDasPaginas {
  /** Ainda buscando páginas: a contagem da fila não é definitiva. */
  carregandoTudo: boolean;
  /** Parou no teto com mais registros na API: a fila cobre só os mais recentes. */
  truncado: boolean;
  /** Quantas páginas já vieram (para dizer "entre as N mais recentes"). */
  paginas: number;
}

/**
 * Busca as páginas seguintes até o fim (proximoCursor = null) ou o teto. A API pagina do mais
 * recente para o mais antigo, e as filas "renovação em 30 dias" e "expirando em 48 h" procuram
 * justamente os registros antigos: filtrar só a 1ª página daria um vazio ou uma contagem falsa.
 */
export function useTodasAsPaginas(
  consulta: ConsultaPaginada,
  ativo: boolean,
  tetoDePaginas = TETO_PAGINAS_FILA,
): ProgressoDasPaginas {
  const { hasNextPage, isFetchingNextPage, isFetchNextPageError, isPending, fetchNextPage } =
    consulta;
  const paginas = consulta.data?.pages.length ?? 0;
  const falta = ativo && hasNextPage && paginas < tetoDePaginas;
  // Uma falha na página seguinte para a busca automática (a lista mostra o "Carregar mais").
  const buscar = falta && !isFetchingNextPage && !isFetchNextPageError;

  useEffect(() => {
    if (buscar) void fetchNextPage();
  }, [buscar, fetchNextPage]);

  return {
    carregandoTudo: ativo && (isPending || (falta && !isFetchNextPageError)),
    truncado: ativo && hasNextPage && paginas >= tetoDePaginas,
    paginas,
  };
}

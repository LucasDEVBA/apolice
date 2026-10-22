import type {
  CodigoProduto,
  CotacaoRequest,
  EndossoRequest,
  ListarApolicesQuery,
  ListarCotacoesQuery,
  ListarSinistrosQuery,
  MotivoCancelamento,
} from "../contratos";

export type FiltrosCotacoes = Omit<ListarCotacoesQuery, "cursor" | "limite">;
export type FiltrosApolices = Omit<ListarApolicesQuery, "cursor" | "limite">;
export type FiltrosSinistros = Omit<ListarSinistrosQuery, "cursor" | "limite">;

/**
 * Fábrica única de query keys (hierárquicas): invalidar `cotacoes.listas()` alcança todas as
 * listas filtradas; `cotacoes.todas` alcança listas, detalhes e simulações.
 */
export const chaves = {
  features: ["features"] as const,
  dashboard: ["dashboard"] as const,
  produtos: {
    todos: ["produtos"] as const,
    lista: () => [...chaves.produtos.todos, "lista"] as const,
    detalhe: (codigo: CodigoProduto) => [...chaves.produtos.todos, "detalhe", codigo] as const,
  },
  cotacoes: {
    todas: ["cotacoes"] as const,
    listas: () => [...chaves.cotacoes.todas, "lista"] as const,
    lista: (filtros: FiltrosCotacoes) => [...chaves.cotacoes.listas(), filtros] as const,
    detalhe: (id: string) => [...chaves.cotacoes.todas, "detalhe", id] as const,
    simulacao: (pedido: CotacaoRequest) => [...chaves.cotacoes.todas, "simulacao", pedido] as const,
  },
  apolices: {
    todas: ["apolices"] as const,
    listas: () => [...chaves.apolices.todas, "lista"] as const,
    lista: (filtros: FiltrosApolices) => [...chaves.apolices.listas(), filtros] as const,
    detalhes: () => [...chaves.apolices.todas, "detalhe"] as const,
    detalhe: (id: string) => [...chaves.apolices.detalhes(), id] as const,
    simulacaoEndosso: (id: string, pedido: EndossoRequest) =>
      [...chaves.apolices.todas, "simulacao-endosso", id, pedido] as const,
    simulacaoCancelamento: (id: string, motivo: MotivoCancelamento) =>
      [...chaves.apolices.todas, "simulacao-cancelamento", id, motivo] as const,
  },
  sinistros: {
    todos: ["sinistros"] as const,
    listas: () => [...chaves.sinistros.todos, "lista"] as const,
    lista: (filtros: FiltrosSinistros) => [...chaves.sinistros.listas(), filtros] as const,
    detalhe: (id: string) => [...chaves.sinistros.todos, "detalhe", id] as const,
  },
  auditoria: {
    todas: ["auditoria"] as const,
    lista: () => [...chaves.auditoria.todas, "lista"] as const,
  },
} as const;

/** Tamanho de página das listas (a API aceita 1–100; padrão 20). */
export const TAMANHO_PAGINA = 20;
/** Paginacao.LimiteMaximo da API: as filas que filtram no cliente leem de 100 em 100. */
export const LIMITE_MAXIMO_PAGINA = 100;

import type {
  CotacaoDto,
  CotacaoRequest,
  CotacaoResumoDto,
  ListarCotacoesQuery,
  Pagina,
  TarifacaoDto,
} from "../contratos";
import { requisitar } from "../http";

/** POST /cotacoes/simular — não persiste (simulação ao vivo do assistente). */
export function simularCotacao(corpo: CotacaoRequest, sinal?: AbortSignal): Promise<TarifacaoDto> {
  return requisitar("/cotacoes/simular", { metodo: "POST", corpo, sinal });
}

/** POST /cotacoes → 201 */
export function criarCotacao(corpo: CotacaoRequest): Promise<CotacaoDto> {
  return requisitar("/cotacoes", { metodo: "POST", corpo });
}

/** GET /cotacoes?status&produto&busca&cursor&limite */
export function listarCotacoes(
  query: ListarCotacoesQuery,
  sinal?: AbortSignal,
): Promise<Pagina<CotacaoResumoDto>> {
  return requisitar("/cotacoes", { query: { ...query }, sinal });
}

/** GET /cotacoes/{id} */
export function obterCotacao(id: string, sinal?: AbortSignal): Promise<CotacaoDto> {
  return requisitar(`/cotacoes/${encodeURIComponent(id)}`, { sinal });
}

/** POST /cotacoes/{id}/aceitar */
export function aceitarCotacao(id: string): Promise<CotacaoDto> {
  return requisitar(`/cotacoes/${encodeURIComponent(id)}/aceitar`, { metodo: "POST" });
}

/** POST /cotacoes/{id}/recusar */
export function recusarCotacao(id: string): Promise<CotacaoDto> {
  return requisitar(`/cotacoes/${encodeURIComponent(id)}/recusar`, { metodo: "POST" });
}

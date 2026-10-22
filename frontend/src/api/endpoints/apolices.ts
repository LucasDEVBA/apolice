import type {
  ApoliceDto,
  ApoliceResumoDto,
  CancelamentoRequest,
  EmitirApoliceRequest,
  EndossoRequest,
  ListarApolicesQuery,
  Pagina,
  SimulacaoCancelamentoDto,
  SimulacaoEndossoDto,
} from "../contratos";
import { requisitar } from "../http";

const caminhoDa = (id: string) => `/apolices/${encodeURIComponent(id)}`;

/** POST /apolices → 201 (Idempotency-Key: uma chave por submissão do diálogo de emissão). */
export function emitirApolice(
  corpo: EmitirApoliceRequest,
  chaveIdempotencia: string,
): Promise<ApoliceDto> {
  return requisitar("/apolices", { metodo: "POST", corpo, chaveIdempotencia });
}

/** GET /apolices?status&produto&busca&cursor&limite */
export function listarApolices(
  query: ListarApolicesQuery,
  sinal?: AbortSignal,
): Promise<Pagina<ApoliceResumoDto>> {
  return requisitar("/apolices", { query: { ...query }, sinal });
}

/** GET /apolices/{id} — com endossos, sinistros, notificações e cronograma. */
export function obterApolice(id: string, sinal?: AbortSignal): Promise<ApoliceDto> {
  return requisitar(caminhoDa(id), { sinal });
}

/** GET /apolices/{id}/documento — PDF (flag DocumentoPdf), baixado como blob com o token. */
export function baixarDocumentoApolice(id: string): Promise<Blob> {
  return requisitar(`${caminhoDa(id)}/documento`, { resposta: "blob" });
}

/** POST /apolices/{id}/endossos/simular (flag EndossoOnline) */
export function simularEndosso(
  id: string,
  corpo: EndossoRequest,
  sinal?: AbortSignal,
): Promise<SimulacaoEndossoDto> {
  return requisitar(`${caminhoDa(id)}/endossos/simular`, { metodo: "POST", corpo, sinal });
}

/** POST /apolices/{id}/endossos → 201 ApoliceDto */
export function endossarApolice(id: string, corpo: EndossoRequest): Promise<ApoliceDto> {
  return requisitar(`${caminhoDa(id)}/endossos`, { metodo: "POST", corpo });
}

/** POST /apolices/{id}/cancelamento/simular */
export function simularCancelamento(
  id: string,
  corpo: CancelamentoRequest,
  sinal?: AbortSignal,
): Promise<SimulacaoCancelamentoDto> {
  return requisitar(`${caminhoDa(id)}/cancelamento/simular`, { metodo: "POST", corpo, sinal });
}

/** POST /apolices/{id}/cancelamento → 200 ApoliceDto */
export function cancelarApolice(id: string, corpo: CancelamentoRequest): Promise<ApoliceDto> {
  return requisitar(`${caminhoDa(id)}/cancelamento`, { metodo: "POST", corpo });
}

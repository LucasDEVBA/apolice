import type { AtualizarTaxaRequest, CodigoProduto, ProdutoDto } from "../contratos";
import { requisitar } from "../http";

/** GET /produtos (cache no servidor). */
export function listarProdutos(sinal?: AbortSignal): Promise<ProdutoDto[]> {
  return requisitar("/produtos", { sinal });
}

/** GET /produtos/{codigo} */
export function obterProduto(codigo: CodigoProduto, sinal?: AbortSignal): Promise<ProdutoDto> {
  return requisitar(`/produtos/${encodeURIComponent(codigo)}`, { sinal });
}

/** PUT /produtos/{codigo}/coberturas/{coberturaCodigo}/taxa — Admin. */
export function atualizarTaxa(
  codigo: CodigoProduto,
  coberturaCodigo: string,
  corpo: AtualizarTaxaRequest,
): Promise<ProdutoDto> {
  return requisitar(
    `/produtos/${encodeURIComponent(codigo)}/coberturas/${encodeURIComponent(coberturaCodigo)}/taxa`,
    { metodo: "PUT", corpo },
  );
}

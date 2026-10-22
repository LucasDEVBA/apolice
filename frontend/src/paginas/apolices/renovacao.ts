import type { ApoliceResumoDto } from "@/api/contratos";
import { diasEntre, hojeEmBrasilia } from "@/ui/utils/datas";

export const DIAS_JANELA_RENOVACAO = 30;

/** Vigência que termina nos próximos 30 dias (fila "Renovações em 30 dias"). */
export function venceEmAte30Dias(
  apolice: Pick<ApoliceResumoDto, "vigenciaFim" | "status">,
  hoje = hojeEmBrasilia(),
): boolean {
  const dias = diasEntre(hoje, apolice.vigenciaFim);
  return apolice.status === "Ativa" && dias >= 0 && dias <= DIAS_JANELA_RENOVACAO;
}

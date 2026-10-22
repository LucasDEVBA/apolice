import type { AuditoriaDto, ListarAuditoriaQuery, Pagina } from "../contratos";
import { requisitar } from "../http";

/** GET /admin/auditoria?cursor&limite — Admin. */
export function listarAuditoria(
  query: ListarAuditoriaQuery,
  sinal?: AbortSignal,
): Promise<Pagina<AuditoriaDto>> {
  return requisitar("/admin/auditoria", { query: { ...query }, sinal });
}

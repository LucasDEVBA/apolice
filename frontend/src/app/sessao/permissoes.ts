import type { Papel } from "@/api/contratos";

/**
 * O que cada papel pode fazer na interface. Espelha as políticas da API (SPEC §7): a UI só esconde
 * o que o backend recusaria; quem decide de verdade é a API (RBAC + autorização por recurso).
 */
export type Permissao = "operarCarteira" | "analisarSinistros" | "administrar";

const PAPEIS_POR_PERMISSAO: Record<Permissao, readonly Papel[]> = {
  // Criar cotação, aceitar/recusar, emitir, endossar, cancelar e abrir sinistro.
  operarCarteira: ["Corretor", "Admin"],
  // Iniciar análise, aprovar, negar e registrar pagamento de sinistro.
  analisarSinistros: ["Analista", "Admin"],
  administrar: ["Admin"],
};

export const PERFIS_DA_PERMISSAO: Record<Permissao, string> = {
  operarCarteira: "Corretor ou Admin",
  analisarSinistros: "Analista ou Admin",
  administrar: "Admin",
};

export function pode(papel: Papel | undefined, permissao: Permissao): boolean {
  return papel !== undefined && PAPEIS_POR_PERMISSAO[permissao].includes(papel);
}

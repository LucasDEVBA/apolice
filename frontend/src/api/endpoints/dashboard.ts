import type { DashboardDto } from "../contratos";
import { requisitar } from "../http";

/** GET /dashboard — escopo Carteira (corretor) ou Global (analista/admin). */
export function obterDashboard(sinal?: AbortSignal): Promise<DashboardDto> {
  return requisitar("/dashboard", { sinal });
}

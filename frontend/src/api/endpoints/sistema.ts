import type { FeaturesDto } from "../contratos";
import { requisitar } from "../http";

/** GET /features — anônimo: a tela de login decide se mostra as personas de demonstração. */
export function obterFeatures(sinal?: AbortSignal): Promise<FeaturesDto> {
  return requisitar("/features", { sinal, anonima: true });
}

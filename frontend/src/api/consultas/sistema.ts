import { useQuery } from "@tanstack/react-query";

import { obterDashboard } from "../endpoints/dashboard";
import { obterFeatures } from "../endpoints/sistema";
import { chaves } from "./chaves";

const MINUTOS_FEATURES = 5;

/** GET /features — flags de login demo, PDF, endosso e triagem (mudam raramente). */
export function useFeatures() {
  return useQuery({
    queryKey: chaves.features,
    queryFn: ({ signal }) => obterFeatures(signal),
    staleTime: MINUTOS_FEATURES * 60_000,
  });
}

/** GET /dashboard — também alimenta os contadores da navegação. */
export function useDashboard(habilitado = true) {
  return useQuery({
    queryKey: chaves.dashboard,
    queryFn: ({ signal }) => obterDashboard(signal),
    enabled: habilitado,
  });
}

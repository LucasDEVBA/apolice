import { useDashboard } from "@/api/consultas/sistema";

/**
 * Contadores da navegação a partir do GET /dashboard (a API não tem contagem por lista): cotações
 * em aberto, apólices ativas e sinistros em aberto, no escopo de quem está logado.
 */
export function useContadoresNavegacao(): Partial<Record<string, number>> {
  const { data } = useDashboard();
  if (!data) return {};
  return {
    "/cotacoes": data.cotacoesAbertas,
    "/apolices": data.apolicesAtivas,
    "/sinistros": data.sinistrosEmAberto,
  };
}

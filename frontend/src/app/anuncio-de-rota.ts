import { createContext, useContext, useEffect, type RefObject } from "react";
import { useLocation } from "react-router";

const NOME_APP = "Apólice";

export interface NavegacaoDaSessao {
  /**
   * Registra o caminho atual e diz se a sessão já saiu da primeira tela. Depois da primeira troca
   * de caminho a resposta fica true para sempre: daí em diante toda tela anuncia.
   */
  jaNavegou: (caminho: string) => boolean;
}

export const ContextoNavegacao = createContext<NavegacaoDaSessao | null>(null);

/**
 * Anúncio de rota (WCAG 2.4.3): atualiza document.title e, depois da primeira navegação, move o
 * foco para o H1 da tela, que o leitor de tela lê. Na carga do documento o foco fica no início
 * (o skip link é o primeiro Tab). Decide pelo caminho, não pelo título: um título que chega
 * depois do fetch não rouba o foco de quem já está interagindo, e filtros na query string
 * (mesmo caminho) também não.
 */
export function useAnuncioDeRota(titulo: string, alvo: RefObject<HTMLElement | null>) {
  const { pathname } = useLocation();
  const navegacao = useContext(ContextoNavegacao);

  useEffect(() => {
    document.title = `${titulo} · ${NOME_APP}`;
  }, [titulo]);

  useEffect(() => {
    // Sem provedor (componente isolado em teste, erro na raiz): não mexe no foco.
    if (!navegacao?.jaNavegou(pathname)) return;
    alvo.current?.querySelector<HTMLElement>("h1")?.focus();
  }, [pathname, navegacao, alvo]);
}

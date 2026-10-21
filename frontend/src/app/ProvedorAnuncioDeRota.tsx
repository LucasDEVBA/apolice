import { useState, type ReactNode } from "react";
import { useLocation } from "react-router";

import { ContextoNavegacao, type NavegacaoDaSessao } from "./anuncio-de-rota";

/**
 * Guarda, por instância do router, onde a sessão começou. Estado de módulo vazaria entre testes
 * e entre as montagens duplas do StrictMode; aqui cada app (ou teste) tem o seu.
 */
export function ProvedorAnuncioDeRota({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  const [navegacao] = useState<NavegacaoDaSessao>(() => {
    const caminhoInicial = pathname;
    let saiuDaPrimeiraTela = false;
    return {
      jaNavegou: (caminho) => {
        if (caminho !== caminhoInicial) saiuDaPrimeiraTela = true;
        return saiuDaPrimeiraTela;
      },
    };
  });
  return <ContextoNavegacao value={navegacao}>{children}</ContextoNavegacao>;
}

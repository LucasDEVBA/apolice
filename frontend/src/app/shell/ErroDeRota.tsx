import { useEffect, useRef } from "react";
import { isRouteErrorResponse, useLocation, useNavigate, useRouteError } from "react-router";

import { Card } from "@/ui/componentes/Card";
import { ErrorState } from "@/ui/componentes/ErrorState";

import { useAnuncioDeRota } from "../anuncio-de-rota";

const HTTP_NAO_ENCONTRADO = 404;
// Chrome/Edge, Firefox e Safari, nessa ordem, quando o chunk de uma tela não carrega.
const FALHA_DE_CHUNK =
  /Failed to fetch dynamically imported module|error loading dynamically imported module|Importing a module script failed/i;

/**
 * Chunk que não carregou (deploy novo com a aba antiga aberta, rede): o React.lazy guarda a
 * promessa rejeitada, então navegar de novo repetiria o mesmo erro. Só recarregar resolve.
 */
function ehFalhaDeChunk(erro: unknown): boolean {
  return (
    erro instanceof Error && (erro.name === "ChunkLoadError" || FALHA_DE_CHUNK.test(erro.message))
  );
}

/** ErrorBoundary de rota: o erro fica contido no conteúdo, com o shell e a navegação de pé. */
export function ErroDeRota() {
  const erro = useRouteError();
  const navegar = useNavigate();
  const { pathname, search } = useLocation();
  const ref = useRef<HTMLDivElement>(null);
  const naoEncontrada = isRouteErrorResponse(erro) && erro.status === HTTP_NAO_ENCONTRADO;
  const titulo = naoEncontrada ? "Página não encontrada" : "Algo deu errado nesta tela";
  useAnuncioDeRota(titulo, ref);

  // Programador: o detalhe vai para o console uma vez por erro (não a cada render).
  useEffect(() => {
    console.error({ nivel: "erro", acao: "erro_de_rota", rota: pathname, erro });
  }, [erro, pathname]);

  function tentarDeNovo() {
    if (ehFalhaDeChunk(erro)) {
      window.location.reload();
      return;
    }
    void navegar(`${pathname}${search}`, { replace: true });
  }

  return (
    <div ref={ref}>
      <Card className="mt-2">
        <ErrorState
          titulo={titulo}
          nivel="h1"
          descricao="O restante do sistema continua funcionando. Tente carregar esta tela de novo."
          aoTentarDeNovo={tentarDeNovo}
        />
      </Card>
    </div>
  );
}

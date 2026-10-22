import { useSearchParams } from "react-router";

export function lerOpcao<T extends string>(valor: string | null, opcoes: readonly T[]): T | null {
  return opcoes.find((o) => o === valor) ?? null;
}

/**
 * Filtros da lista na URL (status, produto, busca, fila): o link de uma fila salva ou de um KPI
 * abre a lista já filtrada, e voltar no navegador desfaz o filtro.
 */
export function useFiltrosDaUrl() {
  const [params, setParams] = useSearchParams();
  function definir(chave: string, valor: string | null) {
    setParams(
      (atual) => {
        const proximo = new URLSearchParams(atual);
        if (valor === null || valor === "") proximo.delete(chave);
        else proximo.set(chave, valor);
        return proximo;
      },
      { replace: true },
    );
  }
  function limpar() {
    setParams(new URLSearchParams(), { replace: true });
  }
  return { params, definir, limpar };
}

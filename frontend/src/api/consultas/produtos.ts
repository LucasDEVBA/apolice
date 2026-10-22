import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import type { CodigoProduto, ProdutoDto } from "../contratos";
import { atualizarTaxa, listarProdutos } from "../endpoints/produtos";
import { chaves } from "./chaves";

const MINUTOS_CATALOGO = 10;

/** Catálogo (GET /produtos): o servidor também guarda 10 min em cache. */
export function useProdutos() {
  return useQuery({
    queryKey: chaves.produtos.lista(),
    queryFn: ({ signal }) => listarProdutos(signal),
    staleTime: MINUTOS_CATALOGO * 60_000,
  });
}

export function useProduto(codigo: CodigoProduto | undefined): ProdutoDto | undefined {
  const { data } = useProdutos();
  return codigo ? data?.find((p) => p.codigo === codigo) : undefined;
}

interface VariaveisTaxa {
  produto: CodigoProduto;
  cobertura: string;
  taxa: number;
}

/** PUT da taxa (Admin): invalida o catálogo, que é a base da tarifação no assistente. */
export function useAtualizarTaxa() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ produto, cobertura, taxa }: VariaveisTaxa) =>
      atualizarTaxa(produto, cobertura, { taxa }),
    meta: { errosNoFormulario: true },
    onSuccess: (produto) => {
      queryClient.setQueryData<ProdutoDto[]>(chaves.produtos.lista(), (atual) =>
        atual?.map((p) => (p.codigo === produto.codigo ? produto : p)),
      );
      return queryClient.invalidateQueries({ queryKey: chaves.produtos.todos });
    },
  });
}

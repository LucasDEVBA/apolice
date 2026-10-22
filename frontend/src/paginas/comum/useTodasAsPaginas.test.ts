import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { useTodasAsPaginas } from "./useTodasAsPaginas";

function consulta(paginas: number, temMais: boolean) {
  return {
    data: { pages: Array.from({ length: paginas }, () => ({})) },
    isPending: false,
    hasNextPage: temMais,
    isFetchingNextPage: false,
    isFetchNextPageError: false,
    fetchNextPage: vi.fn(() => Promise.resolve()),
  };
}

describe("useTodasAsPaginas", () => {
  it("deve buscar a página seguinte enquanto houver cursor e a fila estiver ativa", () => {
    const c = consulta(1, true);
    const { result } = renderHook(() => useTodasAsPaginas(c, true, 3));
    expect(c.fetchNextPage).toHaveBeenCalledTimes(1);
    expect(result.current).toMatchObject({ carregandoTudo: true, truncado: false });
  });

  it("não deve buscar mais páginas fora da fila", () => {
    const c = consulta(1, true);
    const { result } = renderHook(() => useTodasAsPaginas(c, false, 3));
    expect(c.fetchNextPage).not.toHaveBeenCalled();
    expect(result.current).toMatchObject({ carregandoTudo: false, truncado: false });
  });

  it("deve parar no teto e avisar que a fila cobre só os mais recentes", () => {
    const c = consulta(3, true);
    const { result } = renderHook(() => useTodasAsPaginas(c, true, 3));
    expect(c.fetchNextPage).not.toHaveBeenCalled();
    expect(result.current).toMatchObject({ carregandoTudo: false, truncado: true, paginas: 3 });
  });

  it("deve parar a busca automática quando a página seguinte falhar", () => {
    const c = { ...consulta(1, true), isFetchNextPageError: true };
    const { result } = renderHook(() => useTodasAsPaginas(c, true, 3));
    expect(c.fetchNextPage).not.toHaveBeenCalled();
    expect(result.current.carregandoTudo).toBe(false);
  });
});

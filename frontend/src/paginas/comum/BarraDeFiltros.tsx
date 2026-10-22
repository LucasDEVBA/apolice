import type { ReactNode } from "react";

import { BarraFerramentasTabela, CampoBusca, ChipFiltro } from "@/ui/componentes/FiltrosTabela";

interface GrupoChipsProps<T extends string> {
  rotulo: string;
  opcoes: readonly { valor: T; rotulo: string }[];
  ativo: T | null;
  aoMudar: (valor: T | null) => void;
}

/** Chips de escolha única: clicar no ativo limpa o filtro. */
export function GrupoChips<T extends string>({
  rotulo,
  opcoes,
  ativo,
  aoMudar,
}: GrupoChipsProps<T>) {
  return (
    <div role="group" aria-label={rotulo} className="flex flex-wrap items-center gap-2">
      {opcoes.map((opcao) => (
        <ChipFiltro
          key={opcao.valor}
          ativo={ativo === opcao.valor}
          onClick={() => aoMudar(ativo === opcao.valor ? null : opcao.valor)}
        >
          {opcao.rotulo}
        </ChipFiltro>
      ))}
    </div>
  );
}

interface BarraDeFiltrosProps {
  rotuloBusca: string;
  placeholderBusca: string;
  busca: string;
  aoBuscar: (termo: string) => void;
  children?: ReactNode;
}

/** Busca com debounce de 300 ms + grupos de chips (DESIGN.md §4.5). */
export function BarraDeFiltros({
  rotuloBusca,
  placeholderBusca,
  busca,
  aoBuscar,
  children,
}: BarraDeFiltrosProps) {
  return (
    <BarraFerramentasTabela className="pt-4 md:pt-5">
      <CampoBusca
        rotulo={rotuloBusca}
        placeholder={placeholderBusca}
        valorInicial={busca}
        aoBuscar={aoBuscar}
        maxLength={100}
      />
      {children}
    </BarraFerramentasTabela>
  );
}

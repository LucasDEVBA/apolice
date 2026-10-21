import { ChevronDown } from "lucide-react";

import { formatarNumero } from "../utils/formatar";
import { Button } from "./Button";

interface PaginationProps {
  /** Quantos itens já estão na tela. */
  exibidos: number;
  /** Total conhecido (quando a API informar); sem ele, mostra só os exibidos. */
  total?: number | undefined;
  temMais: boolean;
  carregando?: boolean;
  aoCarregarMais: () => void;
  /** "apólices", "cotações"… */
  substantivo?: string;
}

/** Paginação por cursor, coerente com o SPEC: "Mostrando 25 de 1.284" + "Carregar mais". */
export function Pagination({
  exibidos,
  total,
  temMais,
  carregando = false,
  aoCarregarMais,
  substantivo = "registros",
}: PaginationProps) {
  const texto =
    total === undefined
      ? `Mostrando ${formatarNumero(exibidos)} ${substantivo}`
      : `Mostrando ${formatarNumero(exibidos)} de ${formatarNumero(total)} ${substantivo}`;
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-4 sm:flex-row sm:justify-between md:px-6">
      <p className="type-meta text-ink-3 tnum" aria-live="polite">
        {texto}
      </p>
      {temMais ? (
        <Button
          variante="secundario"
          tamanho="sm"
          icone={ChevronDown}
          carregando={carregando}
          rotuloCarregando="Carregando…"
          onClick={aoCarregarMais}
        >
          Carregar mais
        </Button>
      ) : (
        <p className="type-meta text-ink-3">Fim da lista</p>
      )}
    </div>
  );
}

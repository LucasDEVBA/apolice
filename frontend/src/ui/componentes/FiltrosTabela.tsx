import { Search } from "lucide-react";
import { useEffect, useRef, useState, type ComponentProps, type ReactNode } from "react";

import { cn } from "../utils/cn";
import { formatarNumero } from "../utils/formatar";
import { Input } from "./Input";

const ATRASO_BUSCA_MS = 300;

/** Linha da barra de ferramentas da tabela: busca à esquerda, chips e ações (DESIGN.md §4.5). */
export function BarraFerramentasTabela({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex flex-col gap-3 px-4 pb-4 md:flex-row md:flex-wrap md:items-center md:px-(--card-pad)",
        className,
      )}
    >
      {children}
    </div>
  );
}

interface CampoBuscaProps extends Omit<
  ComponentProps<typeof Input>,
  "value" | "onChange" | "defaultValue" | "type"
> {
  /** Recebe o termo 300 ms depois da última tecla (e na hora, ao limpar o campo). */
  aoBuscar: (termo: string) => void;
  valorInicial?: string;
  /** Nome acessível (o campo não tem rótulo visível). */
  rotulo: string;
}

/** Busca com debounce de 300 ms: a lista não refiltra (nem consulta a API) a cada tecla. */
export function CampoBusca({
  aoBuscar,
  valorInicial = "",
  rotulo,
  classeCaixa,
  ...resto
}: CampoBuscaProps) {
  const [termo, setTermo] = useState(valorInicial);
  const aoBuscarRef = useRef(aoBuscar);

  useEffect(() => {
    aoBuscarRef.current = aoBuscar;
  }, [aoBuscar]);

  useEffect(() => {
    const atraso = termo === "" ? 0 : ATRASO_BUSCA_MS;
    const timer = window.setTimeout(() => aoBuscarRef.current(termo.trim()), atraso);
    return () => window.clearTimeout(timer);
  }, [termo]);

  return (
    <Input
      {...resto}
      type="search"
      aria-label={rotulo}
      icone={Search}
      tamanho="sm"
      value={termo}
      onChange={(e) => setTermo(e.target.value)}
      // 44 px no mobile (alvo de toque), 40 px na barra do desktop.
      classeCaixa={cn("h-11 w-full md:h-10 md:w-72", classeCaixa)}
    />
  );
}

interface ChipFiltroProps extends Omit<ComponentProps<"button">, "children" | "aria-pressed"> {
  ativo: boolean;
  children: ReactNode;
  /** Quantidade de itens com esse filtro (opcional). */
  contador?: number;
}

/**
 * Filtro em pílula com aria-pressed. Desenho de 32 px, alvo de 40 px por pseudo-elemento
 * (DESIGN.md §7). Ativo em --surface-selected, nunca só por cor: ganha borda e peso.
 */
export function ChipFiltro({
  ativo,
  children,
  contador,
  type = "button",
  className,
  ...resto
}: ChipFiltroProps) {
  return (
    <button
      type={type}
      aria-pressed={ativo}
      className={cn(
        "relative inline-flex h-8 shrink-0 items-center gap-1.5 rounded-full border px-3 text-label whitespace-nowrap",
        "transition-colors duration-(--dur-fast) before:absolute before:inset-x-0 before:-inset-y-1",
        ativo
          ? "border-link bg-surface-selected font-semibold text-ink"
          : "border-line bg-surface font-medium text-ink-2 hover:border-line-strong hover:text-ink",
        className,
      )}
      {...resto}
    >
      {children}
      {contador !== undefined && (
        <span className={cn("tnum", ativo ? "text-ink-2" : "text-ink-3")}>
          {/* O gap do flex não vira espaço no nome acessível: "Ativa, 12", não "Ativa12". */}
          <span className="sr-only">, {formatarNumero(contador)}</span>
          <span aria-hidden>{formatarNumero(contador)}</span>
        </span>
      )}
    </button>
  );
}

import { Tabs as RadixTabs } from "radix-ui";
import { useLayoutEffect, useRef, useState, type ReactNode } from "react";

import { cn } from "../utils/cn";

export interface Aba {
  valor: string;
  rotulo: string;
  /** Contador em pílula neutra ("Coberturas 6"). */
  contador?: number;
  conteudo: ReactNode;
  desabilitada?: boolean;
}

interface TabsProps {
  abas: readonly Aba[];
  /** Nome acessível da lista de abas. */
  rotulo: string;
  valor?: string;
  aoMudar?: (valor: string) => void;
  valorPadrao?: string;
  className?: string;
}

/**
 * Abas Radix (setas navegam, Home/End vão às pontas) com sublinhado de 2 px que desliza só com
 * transform (DESIGN.md §4.13/§6). No mobile a lista rola na horizontal com máscara de fade.
 */
export function Tabs({ abas, rotulo, valor, aoMudar, valorPadrao, className }: TabsProps) {
  const [interno, setInterno] = useState(valorPadrao ?? abas[0]?.valor ?? "");
  const atual = valor ?? interno;
  const listaRef = useRef<HTMLDivElement>(null);
  const indicadorRef = useRef<HTMLSpanElement>(null);

  // Posiciona o indicador direto no DOM (sem estado): nada re-renderiza durante a medição.
  useLayoutEffect(() => {
    const lista = listaRef.current;
    const indicador = indicadorRef.current;
    if (!lista || !indicador) return;
    const posicionar = () => {
      const ativa = lista.querySelector<HTMLElement>('[role="tab"][data-state="active"]');
      if (!ativa) return;
      indicador.style.transform = `translateX(${String(ativa.offsetLeft)}px) scaleX(${String(ativa.offsetWidth)})`;
      indicador.style.opacity = "1";
      // Rola só a lista (nunca a página) para a aba ativa ficar visível no mobile.
      const direita = ativa.offsetLeft + ativa.offsetWidth;
      if (ativa.offsetLeft < lista.scrollLeft) lista.scrollLeft = ativa.offsetLeft;
      else if (direita > lista.scrollLeft + lista.clientWidth)
        lista.scrollLeft = direita - lista.clientWidth;
    };
    posicionar();
    if (typeof ResizeObserver === "undefined") return;
    const observador = new ResizeObserver(posicionar);
    observador.observe(lista);
    return () => observador.disconnect();
  }, [atual]);

  function mudar(novo: string) {
    setInterno(novo);
    aoMudar?.(novo);
  }

  return (
    <RadixTabs.Root value={atual} onValueChange={mudar} className={cn("min-w-0", className)}>
      <div className="relative border-b border-line">
        <RadixTabs.List
          ref={listaRef}
          aria-label={rotulo}
          className="relative scrollbar-none flex gap-1 overflow-x-auto fade-x pr-6 md:gap-2"
        >
          {abas.map((aba) => (
            <RadixTabs.Trigger
              key={aba.valor}
              value={aba.valor}
              disabled={aba.desabilitada ?? false}
              className={cn(
                "inline-flex h-11 shrink-0 items-center gap-2 px-3 text-sm font-medium whitespace-nowrap text-ink-2",
                "transition-colors duration-(--dur-fast) hover:text-ink focus-visible:-outline-offset-2",
                "data-[state=active]:font-semibold data-[state=active]:text-ink max-md:data-[state=active]:font-bold",
                "disabled:cursor-not-allowed disabled:text-ink-3",
              )}
            >
              {aba.rotulo}
              {aba.contador !== undefined && (
                <span className="inline-grid h-5 min-w-5 place-items-center rounded-full bg-neutral-bg px-1.5 text-xs font-semibold text-neutral tnum">
                  {/* Separador só para o nome acessível ("Coberturas, 6", não "Coberturas6"). */}
                  <span className="sr-only">, {aba.contador}</span>
                  <span aria-hidden>{aba.contador}</span>
                </span>
              )}
            </RadixTabs.Trigger>
          ))}
          <span
            ref={indicadorRef}
            aria-hidden
            className="pointer-events-none absolute bottom-0 left-0 h-0.5 w-px origin-left bg-primary opacity-0 transition-transform duration-(--dur-base) ease-in-out"
          />
        </RadixTabs.List>
      </div>
      {abas.map((aba) => (
        <RadixTabs.Content
          key={aba.valor}
          value={aba.valor}
          className="pt-5 focus-visible:outline-offset-4"
        >
          {aba.conteudo}
        </RadixTabs.Content>
      ))}
    </RadixTabs.Root>
  );
}

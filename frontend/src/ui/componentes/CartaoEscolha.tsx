import { Check, Lock } from "lucide-react";
import { RadioGroup } from "radix-ui";
import { useId, type ReactNode } from "react";

import { cn } from "../utils/cn";

export interface OpcaoCartao<V extends string> {
  valor: V;
  titulo: string;
  /** Linha de apoio ("Prazo curto: retém 20%", "Faixa de LMI…"). */
  descricao?: ReactNode;
  /** Efeito em R$ da escolha ("+ R$ 120,00"). */
  efeito?: ReactNode;
  /** Fator aplicado ("×0,95"). */
  fator?: ReactNode;
  /** Motivo da indisponibilidade: a opção fica desabilitada e o motivo aparece no card. */
  indisponivel?: string;
}

const COLUNAS = { 1: "", 2: "sm:grid-cols-2", 3: "sm:grid-cols-3" } as const;

interface GrupoCartoesEscolhaProps<V extends string> {
  opcoes: readonly OpcaoCartao<V>[];
  /** "" = nada escolhido ainda. */
  valor: V | "";
  aoMudar: (valor: V) => void;
  /** Nome acessível do grupo (ou aria-labelledby). */
  rotulo?: string;
  "aria-labelledby"?: string;
  colunas?: keyof typeof COLUNAS;
  className?: string;
}

function CartaoEscolha<V extends string>({ opcao }: { opcao: OpcaoCartao<V> }) {
  const id = useId();
  const temDetalhe = opcao.descricao ?? opcao.efeito ?? opcao.fator ?? opcao.indisponivel;
  return (
    <RadioGroup.Item
      value={opcao.valor}
      disabled={opcao.indisponivel !== undefined}
      aria-labelledby={`${id}-titulo`}
      {...(temDetalhe ? { "aria-describedby": `${id}-detalhe` } : {})}
      className={cn(
        "relative flex flex-col items-start gap-1 rounded-lg border border-line bg-surface px-4 py-3.5 pr-10 text-left",
        "transition-[border-color,box-shadow] duration-(--dur-fast) hover:border-line-strong",
        // Selecionado: borda interna de 2 px --primary-bg + check no canto (DESIGN.md §4.10).
        "data-[state=checked]:border-transparent data-[state=checked]:shadow-[inset_0_0_0_2px_var(--primary-bg)]",
        "disabled:cursor-not-allowed disabled:bg-surface-2 disabled:hover:border-line",
      )}
    >
      <RadioGroup.Indicator className="absolute top-3 right-3 grid size-5 place-items-center rounded-full bg-primary text-primary-fg">
        <Check size={13} strokeWidth={2.4} aria-hidden />
      </RadioGroup.Indicator>
      <span
        id={`${id}-titulo`}
        className={cn("text-sm font-semibold", opcao.indisponivel ? "text-ink-3" : "text-ink")}
      >
        {opcao.titulo}
      </span>
      {temDetalhe && (
        <span id={`${id}-detalhe`} className="flex flex-col gap-1">
          {opcao.descricao && <span className="type-meta text-ink-3">{opcao.descricao}</span>}
          {(opcao.efeito ?? opcao.fator) && (
            <span className="flex flex-wrap items-baseline gap-x-2 text-label tnum">
              {opcao.efeito && <span className="font-semibold text-ink">{opcao.efeito}</span>}
              {opcao.fator && <span className="text-ink-2">{opcao.fator}</span>}
            </span>
          )}
          {opcao.indisponivel && (
            <span className="inline-flex items-center gap-1.5 type-meta text-ink-2">
              <Lock size={13} aria-hidden />
              {opcao.indisponivel}
            </span>
          )}
        </span>
      )}
    </RadioGroup.Item>
  );
}

/**
 * Cartões de escolha (DESIGN.md §4.10): RadioGroup do Radix em cards de raio 10, setas trocam a
 * opção. Para escolhas de 2 a 4 opções com preço (produto, tipo de imóvel, motivo).
 */
export function GrupoCartoesEscolha<V extends string>({
  opcoes,
  valor,
  aoMudar,
  rotulo,
  colunas = 1,
  className,
  ...aria
}: GrupoCartoesEscolhaProps<V>) {
  return (
    <RadioGroup.Root
      value={valor}
      onValueChange={(novo) => {
        const opcao = opcoes.find((o) => o.valor === novo);
        if (opcao) aoMudar(opcao.valor);
      }}
      {...(rotulo ? { "aria-label": rotulo } : {})}
      {...aria}
      className={cn("grid gap-2", COLUNAS[colunas], className)}
    >
      {opcoes.map((opcao) => (
        <CartaoEscolha key={opcao.valor} opcao={opcao} />
      ))}
    </RadioGroup.Root>
  );
}

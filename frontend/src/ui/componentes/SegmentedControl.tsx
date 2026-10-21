import { RadioGroup } from "radix-ui";

import { cn } from "../utils/cn";

const ALTURAS = { lg: "h-11", md: "h-10", sm: "h-8" } as const;

export interface OpcaoSegmento<V extends string> {
  valor: V;
  rotulo: string;
  desabilitada?: boolean;
}

export interface SegmentedControlProps<V extends string> {
  opcoes: readonly OpcaoSegmento<V>[];
  valor: V;
  aoMudar: (valor: V) => void;
  /** Nome acessível do grupo (ou use aria-labelledby do FormField). */
  rotulo?: string;
  "aria-labelledby"?: string;
  "aria-describedby"?: string;
  /** lg (44 px) onde o alvo precisa do mínimo do mobile, como o drawer. */
  tamanho?: "lg" | "md" | "sm";
  larguraTotal?: boolean;
  /** Variante sobre marinho (drawer, face da apólice). */
  sobreMarinho?: boolean;
  className?: string;
}

/**
 * Controle segmentado com semântica de radiogroup: Tab entra no grupo, setas trocam a opção
 * (DESIGN.md §4.10). Usos: parcelamento, tipo de imóvel, período do dashboard, tema.
 */
export function SegmentedControl<V extends string>({
  opcoes,
  valor,
  aoMudar,
  rotulo,
  tamanho = "md",
  larguraTotal = false,
  sobreMarinho = false,
  className,
  ...aria
}: SegmentedControlProps<V>) {
  return (
    <RadioGroup.Root
      value={valor}
      onValueChange={(novo) => {
        const opcao = opcoes.find((o) => o.valor === novo);
        if (opcao) aoMudar(opcao.valor);
      }}
      orientation="horizontal"
      loop
      {...(rotulo ? { "aria-label": rotulo } : {})}
      {...aria}
      className={cn(
        "inline-grid auto-cols-fr grid-flow-col gap-[3px] rounded-md border p-[3px]",
        sobreMarinho ? "border-on-navy/22" : "border-control bg-surface",
        ALTURAS[tamanho],
        larguraTotal && "grid w-full",
        className,
      )}
    >
      {opcoes.map((opcao) => (
        <RadioGroup.Item
          key={opcao.valor}
          value={opcao.valor}
          disabled={opcao.desabilitada ?? false}
          className={cn(
            "rounded-xs px-3 text-item font-medium whitespace-nowrap tnum",
            "transition-colors duration-(--dur-base) ease-out data-[state=checked]:font-semibold",
            "focus-visible:outline-offset-1 disabled:cursor-not-allowed",
            sobreMarinho
              ? "text-on-navy-2 hover:text-on-navy focus-visible:outline-gold-line data-[state=checked]:bg-on-navy data-[state=checked]:text-brand"
              : "text-ink-2 hover:text-ink disabled:text-ink-3 data-[state=checked]:bg-primary data-[state=checked]:text-primary-fg",
          )}
        >
          {opcao.rotulo}
        </RadioGroup.Item>
      ))}
    </RadioGroup.Root>
  );
}

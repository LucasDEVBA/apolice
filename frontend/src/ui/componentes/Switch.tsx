import { Lock } from "lucide-react";
import { Switch as RadixSwitch } from "radix-ui";
import type { ComponentProps } from "react";

import { cn } from "../utils/cn";
import { Badge } from "./Badge";

export interface SwitchProps extends Omit<
  ComponentProps<typeof RadixSwitch.Root>,
  "checked" | "onCheckedChange" | "disabled"
> {
  ligado?: boolean;
  aoMudar?: (ligado: boolean) => void;
  desabilitado?: boolean;
  /**
   * Cobertura obrigatória: fica ligada e travada, com o trilho em cor plena (não esmaece, senão
   * parece desligada). Ponha o <SeloObrigatoria /> ao lado do nome (DESIGN.md §4.9).
   */
  obrigatorio?: boolean;
}

/** Trilho 40 × 24, bolinha 18 deslizando 16 px em 220 ms. Espaço alterna (role="switch"). */
export function Switch({
  ligado,
  aoMudar,
  desabilitado = false,
  obrigatorio = false,
  className,
  ...resto
}: SwitchProps) {
  return (
    <RadixSwitch.Root
      {...(ligado === undefined && !obrigatorio ? {} : { checked: obrigatorio || Boolean(ligado) })}
      {...(aoMudar ? { onCheckedChange: aoMudar } : {})}
      disabled={obrigatorio || desabilitado}
      className={cn(
        "group relative inline-flex h-6 w-10 shrink-0 items-center rounded-full bg-control p-[3px]",
        "transition-colors duration-(--dur-toggle) ease-out data-[state=checked]:bg-switch-on",
        "disabled:cursor-not-allowed",
        // Só o desabilitado comum esmaece; a obrigatória mantém o trilho ligado em cor plena.
        !obrigatorio && "data-disabled:opacity-60 data-disabled:data-[state=checked]:opacity-50",
        className,
      )}
      {...resto}
    >
      <RadixSwitch.Thumb
        className={cn(
          "block size-[18px] rounded-full bg-switch-thumb shadow-thumb",
          "transition-transform duration-(--dur-toggle) ease-out data-[state=checked]:translate-x-4 data-[state=checked]:bg-switch-thumb-on",
        )}
      />
    </RadixSwitch.Root>
  );
}

/** Badge neutral "Obrigatória" com cadeado, para ficar ao lado do nome da cobertura. */
export function SeloObrigatoria({ className }: { className?: string }) {
  return (
    <Badge tom="neutral" marcador={Lock} {...(className ? { className } : {})}>
      Obrigatória
    </Badge>
  );
}

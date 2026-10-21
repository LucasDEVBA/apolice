import { Check, Minus } from "lucide-react";
import { Checkbox as RadixCheckbox } from "radix-ui";
import { useId, type ReactNode } from "react";

import { cn } from "../utils/cn";

export interface CheckboxProps {
  rotulo: ReactNode;
  marcado?: boolean | "indeterminate";
  aoMudar?: (marcado: boolean) => void;
  desabilitado?: boolean;
  name?: string;
  id?: string;
  className?: string;
}

export function Checkbox({
  rotulo,
  marcado,
  aoMudar,
  desabilitado = false,
  name,
  id,
  className,
}: CheckboxProps) {
  const gerado = useId();
  const idCampo = id ?? gerado;
  return (
    <div className={cn("inline-flex items-center gap-2", className)}>
      <RadixCheckbox.Root
        id={idCampo}
        {...(marcado === undefined ? {} : { checked: marcado })}
        {...(aoMudar ? { onCheckedChange: (estado) => aoMudar(estado === true) } : {})}
        {...(name ? { name } : {})}
        disabled={desabilitado}
        className={cn(
          "peer grid size-4 shrink-0 place-items-center rounded-[4px] border border-control bg-surface",
          "transition-colors duration-(--dur-fast) data-[state=checked]:border-primary data-[state=checked]:bg-primary",
          "data-[state=indeterminate]:border-primary data-[state=indeterminate]:bg-primary",
          "disabled:cursor-not-allowed disabled:border-control-disabled disabled:bg-surface-2",
          // Alvo de toque de 44 px sem mudar o desenho de 16 px.
          "relative before:absolute before:-inset-3.5 before:content-['']",
        )}
      >
        <RadixCheckbox.Indicator className="text-primary-fg">
          {marcado === "indeterminate" ? (
            <Minus size={12} strokeWidth={2.5} aria-hidden />
          ) : (
            <Check size={12} strokeWidth={2.5} aria-hidden />
          )}
        </RadixCheckbox.Indicator>
      </RadixCheckbox.Root>
      <label
        htmlFor={idCampo}
        className="text-item text-ink-2 peer-disabled:cursor-not-allowed peer-disabled:text-ink-3"
      >
        {rotulo}
      </label>
    </div>
  );
}

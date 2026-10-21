import { Check, ChevronsUpDown } from "lucide-react";
import { Select as RadixSelect } from "radix-ui";

import { useContainerDePortal } from "../tema/container-de-portal";
import { cn } from "../utils/cn";
import { CAIXA_DE_CAMPO, type TamanhoCampo } from "./Input";

export interface OpcaoSelect {
  valor: string;
  rotulo: string;
  desabilitada?: boolean;
}

export interface SelectProps {
  opcoes: readonly OpcaoSelect[];
  valor?: string | undefined;
  aoMudar?: (valor: string) => void;
  valorPadrao?: string;
  placeholder?: string;
  desabilitado?: boolean;
  tamanho?: TamanhoCampo;
  name?: string;
  id?: string;
  className?: string;
  "aria-labelledby"?: string;
  "aria-describedby"?: string;
  "aria-invalid"?: true;
  "aria-required"?: true;
  "aria-label"?: string;
}

/** Radix Select com a mesma caixa do Input e chevron ChevronsUpDown 16 (DESIGN.md §4.8). */
export function Select({
  opcoes,
  valor,
  aoMudar,
  valorPadrao,
  placeholder = "Selecione",
  desabilitado = false,
  tamanho = "md",
  name,
  className,
  ...aria
}: SelectProps) {
  const container = useContainerDePortal();
  return (
    <RadixSelect.Root
      {...(valor === undefined ? {} : { value: valor })}
      {...(valorPadrao === undefined ? {} : { defaultValue: valorPadrao })}
      {...(aoMudar ? { onValueChange: aoMudar } : {})}
      {...(name ? { name } : {})}
      disabled={desabilitado}
    >
      <RadixSelect.Trigger
        {...aria}
        className={cn(
          CAIXA_DE_CAMPO,
          "flex w-full items-center justify-between gap-2 focus-campo px-3.5 text-left text-base tnum md:text-campo",
          "disabled:cursor-not-allowed data-placeholder:text-ink-3",
          tamanho === "md" ? "h-11" : "h-10",
          className,
        )}
      >
        <span className="truncate">
          <RadixSelect.Value placeholder={placeholder} />
        </span>
        <RadixSelect.Icon className="shrink-0 text-ink-2">
          <ChevronsUpDown size={16} aria-hidden />
        </RadixSelect.Icon>
      </RadixSelect.Trigger>
      <RadixSelect.Portal container={container}>
        <RadixSelect.Content
          position="popper"
          sideOffset={4}
          collisionPadding={8}
          className="z-50 max-h-(--radix-select-content-available-height) min-w-(--radix-select-trigger-width) anima-popover overflow-hidden rounded-md border border-line bg-surface-raised text-ink shadow-overlay"
        >
          <RadixSelect.Viewport className="p-1">
            {opcoes.map((opcao) => (
              <RadixSelect.Item
                key={opcao.valor}
                value={opcao.valor}
                disabled={opcao.desabilitada ?? false}
                className={cn(
                  "relative flex h-11 cursor-pointer items-center rounded-[6px] pr-3 pl-8 text-sm text-ink tnum outline-none select-none md:h-10",
                  "data-disabled:cursor-not-allowed data-disabled:text-ink-3 data-highlighted:bg-surface-selected",
                )}
              >
                <RadixSelect.ItemIndicator className="absolute left-2.5 inline-flex text-link">
                  <Check size={15} aria-hidden />
                </RadixSelect.ItemIndicator>
                <RadixSelect.ItemText>{opcao.rotulo}</RadixSelect.ItemText>
              </RadixSelect.Item>
            ))}
          </RadixSelect.Viewport>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  );
}

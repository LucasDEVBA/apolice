import { Check, type LucideIcon } from "lucide-react";
import { DropdownMenu as RadixMenu, Slot } from "radix-ui";
import type { ComponentProps } from "react";

import { useContainerDePortal } from "../tema/container-de-portal";
import { cn } from "../utils/cn";

/**
 * Menu NÃO modal por padrão (padrão menu button da WAI-ARIA): o modal do Radix põe aria-hidden em
 * todo o resto da página, inclusive em elementos focáveis (axe: aria-hidden-focus). Tab ou clique
 * fora fecham o menu do mesmo jeito.
 */
export function DropdownMenu({ modal = false, ...resto }: ComponentProps<typeof RadixMenu.Root>) {
  return <RadixMenu.Root modal={modal} {...resto} />;
}
export const DropdownMenuTrigger = RadixMenu.Trigger;
export const DropdownMenuGroup = RadixMenu.Group;
export const DropdownMenuRadioGroup = RadixMenu.RadioGroup;

const ITEM =
  "relative flex min-h-11 cursor-pointer items-center gap-2.5 rounded-[6px] px-2.5 py-1.5 text-sm text-ink outline-none select-none md:min-h-10 " +
  "data-highlighted:bg-surface-selected data-disabled:cursor-not-allowed data-disabled:text-ink-3";

export function DropdownMenuContent({
  className,
  sideOffset = 6,
  align = "start",
  ...resto
}: ComponentProps<typeof RadixMenu.Content>) {
  const container = useContainerDePortal();
  return (
    <RadixMenu.Portal container={container}>
      <RadixMenu.Content
        sideOffset={sideOffset}
        align={align}
        collisionPadding={8}
        className={cn(
          "z-50 min-w-52 anima-popover rounded-lg border border-line bg-surface-raised p-1 text-ink shadow-overlay",
          className,
        )}
        {...resto}
      />
    </RadixMenu.Portal>
  );
}

interface ItemProps extends ComponentProps<typeof RadixMenu.Item> {
  icone?: LucideIcon;
  perigo?: boolean;
}

export function DropdownMenuItem({
  icone: Icone,
  perigo = false,
  className,
  children,
  asChild,
  ...resto
}: ItemProps) {
  const classes = cn(ITEM, perigo && "text-bad", className);
  const icone = Icone && (
    <Icone size={15} className={perigo ? "text-bad" : "text-ink-2"} aria-hidden />
  );
  if (asChild) {
    // O ícone entra dentro do filho (ex.: <Link>), antes do conteúdo dele.
    return (
      <RadixMenu.Item asChild className={classes} {...resto}>
        <Slot.Root>
          {icone}
          <Slot.Slottable>{children}</Slot.Slottable>
        </Slot.Root>
      </RadixMenu.Item>
    );
  }
  return (
    <RadixMenu.Item className={classes} {...resto}>
      {icone}
      {children}
    </RadixMenu.Item>
  );
}

export function DropdownMenuRadioItem({
  className,
  children,
  ...resto
}: ComponentProps<typeof RadixMenu.RadioItem>) {
  return (
    <RadixMenu.RadioItem className={cn(ITEM, "pl-8", className)} {...resto}>
      <RadixMenu.ItemIndicator className="absolute left-2.5 inline-flex text-link">
        <Check size={15} aria-hidden />
      </RadixMenu.ItemIndicator>
      {children}
    </RadixMenu.RadioItem>
  );
}

export function DropdownMenuCheckboxItem({
  className,
  children,
  ...resto
}: ComponentProps<typeof RadixMenu.CheckboxItem>) {
  return (
    <RadixMenu.CheckboxItem className={cn(ITEM, "pl-8", className)} {...resto}>
      <RadixMenu.ItemIndicator className="absolute left-2.5 inline-flex text-link">
        <Check size={15} aria-hidden />
      </RadixMenu.ItemIndicator>
      {children}
    </RadixMenu.CheckboxItem>
  );
}

export function DropdownMenuLabel({ className, ...resto }: ComponentProps<typeof RadixMenu.Label>) {
  return (
    <RadixMenu.Label
      className={cn("px-2.5 pt-2 pb-1 type-caption text-ink-3", className)}
      {...resto}
    />
  );
}

export function DropdownMenuSeparator({
  className,
  ...resto
}: ComponentProps<typeof RadixMenu.Separator>) {
  return (
    <RadixMenu.Separator className={cn("-mx-1 my-1 h-px bg-line-soft", className)} {...resto} />
  );
}

import { Bell, BellOff } from "lucide-react";
import { Popover } from "radix-ui";

import { useContainerDePortal } from "@/ui/tema/container-de-portal";
import { cn } from "@/ui/utils/cn";
import { formatarNumero } from "@/ui/utils/formatar";

interface NotificacoesProps {
  /** Quantidade não lida; > 0 mostra o ponto --bad e entra no nome acessível. */
  naoLidas?: number;
  /** Sobre o marinho da top bar mobile. */
  sobreMarinho?: boolean;
}

function rotuloDoSino(naoLidas: number): string {
  if (naoLidas <= 0) return "Notificações";
  const sufixo = naoLidas === 1 ? "não lida" : "não lidas";
  return `Notificações, ${formatarNumero(naoLidas)} ${sufixo}`;
}

/**
 * Sino da topbar (DESIGN.md §4.3/§5.1): ponto --bad quando há não lidas e a contagem no nome
 * acessível. Fase 1 sem API: o painel diz o que chega com a integração, sem inventar avisos.
 */
export function Notificacoes({ naoLidas = 0, sobreMarinho = false }: NotificacoesProps) {
  const container = useContainerDePortal();
  return (
    <Popover.Root>
      <Popover.Trigger
        aria-label={rotuloDoSino(naoLidas)}
        className={cn(
          "relative grid shrink-0 place-items-center rounded-md transition-colors duration-(--dur-fast)",
          sobreMarinho
            ? "size-11 text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy focus-visible:outline-gold-line"
            : "size-10 text-ink-2 hover:bg-hover-tint hover:text-ink",
        )}
      >
        <Bell size={18} aria-hidden />
        {naoLidas > 0 && (
          <span
            aria-hidden
            className={cn(
              "absolute top-2 right-2.5 size-2 rounded-full bg-bad ring-2",
              sobreMarinho ? "ring-sidebar" : "ring-surface",
            )}
          />
        )}
      </Popover.Trigger>
      <Popover.Portal container={container}>
        <Popover.Content
          align="end"
          sideOffset={6}
          collisionPadding={8}
          aria-label="Notificações"
          className="z-50 w-80 max-w-[calc(100vw-16px)] anima-popover rounded-lg border border-line bg-surface-raised p-4 text-ink shadow-overlay"
        >
          <p className="type-label text-ink">Notificações</p>
          <div className="mt-3 flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-lg border border-line bg-surface-2 text-ink-2">
              <BellOff size={16} aria-hidden />
            </span>
            <p className="text-label text-ink-2">
              Nenhuma notificação nova. Avisos de emissão, sinistro e renovação chegam com a
              integração da API.
            </p>
          </div>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}

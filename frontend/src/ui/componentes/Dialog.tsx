import { X } from "lucide-react";
import { Dialog as RadixDialog } from "radix-ui";
import { useRef, useState, type ReactElement, type ReactNode } from "react";

import { useContainerDePortal } from "../tema/container-de-portal";
import { cn } from "../utils/cn";
import { Button } from "./Button";

const LARGURAS = {
  confirmacao: "md:max-w-[440px]",
  formulario: "md:max-w-[560px]",
  simulacao: "md:max-w-[720px]",
} as const;

export type LarguraDialog = keyof typeof LARGURAS;

/**
 * Devolve o foco ao elemento que estava focado quando a sobreposição abriu. O Radix só devolve
 * ao Dialog.Trigger; aberta por estado (☰, "Mais", ⌘K), o foco cairia no <body>. Se alguém já
 * moveu o foco enquanto ela fechava (o H1 da página nova, depois de navegar), ele fica onde está.
 */
function useDevolucaoDeFoco() {
  const anterior = useRef<HTMLElement | null>(null);

  function aoAbrir() {
    // Roda antes de o Radix mover o foco para dentro: activeElement ainda é quem abriu.
    const ativo = document.activeElement;
    anterior.current = ativo instanceof HTMLElement && ativo !== document.body ? ativo : null;
  }

  function aoFechar(evento: Event) {
    const ativo = document.activeElement;
    if (ativo instanceof HTMLElement && ativo !== document.body) {
      evento.preventDefault();
      return;
    }
    const alvo = anterior.current;
    anterior.current = null;
    // Sem alvo guardado, segue o padrão do Radix (foco no Dialog.Trigger, se houver).
    if (!alvo?.isConnected) return;
    evento.preventDefault();
    alvo.focus();
  }

  return { aoAbrir, aoFechar };
}

export interface DialogProps {
  aberto?: boolean;
  aoMudar?: (aberto: boolean) => void;
  /** Elemento que abre o diálogo; o foco volta para ele ao fechar. */
  gatilho?: ReactElement;
  titulo: ReactNode;
  descricao?: ReactNode;
  largura?: LarguraDialog;
  /** Ações do rodapé, da secundária para a principal (no mobile a principal sobe para o topo). */
  rodape?: ReactNode;
  children?: ReactNode;
  /** Para onde vai o foco inicial (ex.: a ação segura). Padrão: primeiro campo. */
  aoAbrirFoco?: (evento: Event) => void;
  className?: string;
}

/**
 * Modal Radix (DESIGN.md §4.17): foco preso, Esc fecha e devolve o foco ao gatilho. Abaixo de
 * 768 px vira bottom sheet com alça e ações empilhadas em largura total.
 */
export function Dialog({
  aberto,
  aoMudar,
  gatilho,
  titulo,
  descricao,
  largura = "formulario",
  rodape,
  children,
  aoAbrirFoco,
  className,
}: DialogProps) {
  const container = useContainerDePortal();
  const foco = useDevolucaoDeFoco();
  return (
    <RadixDialog.Root
      {...(aberto === undefined ? {} : { open: aberto })}
      {...(aoMudar ? { onOpenChange: aoMudar } : {})}
    >
      {gatilho && <RadixDialog.Trigger asChild>{gatilho}</RadixDialog.Trigger>}
      <RadixDialog.Portal container={container}>
        <RadixDialog.Overlay className="fixed inset-0 z-50 flex anima-overlay items-end overflow-y-auto bg-overlay md:items-center md:justify-center md:p-6">
          <RadixDialog.Content
            onOpenAutoFocus={(evento) => {
              foco.aoAbrir();
              aoAbrirFoco?.(evento);
            }}
            onCloseAutoFocus={foco.aoFechar}
            {...(descricao ? {} : { "aria-describedby": undefined })}
            className={cn(
              "relative flex max-h-[92dvh] w-full flex-col overflow-hidden bg-surface-raised text-ink shadow-modal outline-none",
              "anima-sheet rounded-t-2xl border-t border-line pb-[env(safe-area-inset-bottom)]",
              "md:max-h-[calc(100dvh-48px)] md:anima-modal md:rounded-2xl md:border md:pb-0",
              LARGURAS[largura],
              className,
            )}
          >
            <span
              aria-hidden
              className="mx-auto mt-2 h-1 w-9 shrink-0 rounded-full bg-line-strong md:hidden"
            />
            <header className="flex items-start gap-4 px-4 pt-4 pr-14 pb-2 md:px-6 md:pt-6 md:pr-16">
              <div className="min-w-0 flex-1">
                <RadixDialog.Title className="type-title-modal text-ink">
                  {titulo}
                </RadixDialog.Title>
                {descricao && (
                  <RadixDialog.Description className="mt-1.5 text-sm text-ink-2">
                    {descricao}
                  </RadixDialog.Description>
                )}
              </div>
            </header>
            {children && (
              <div className="min-h-0 flex-1 overflow-y-auto px-4 pt-2 pb-6 md:px-6">
                {children}
              </div>
            )}
            {rodape && (
              <footer className="flex flex-col-reverse gap-2 border-t border-line bg-surface-2 px-4 py-4 md:flex-row md:justify-end md:px-6 [&>*]:w-full md:[&>*]:w-auto">
                {rodape}
              </footer>
            )}
            {/* Fechar fica por último no DOM: o foco inicial vai para o primeiro campo, não para o X. */}
            <RadixDialog.Close
              aria-label="Fechar"
              className="absolute top-3.5 right-2.5 grid size-11 place-items-center rounded-md text-ink-2 hover:bg-hover-tint hover:text-ink md:top-5 md:right-4 md:size-10"
            >
              <X size={18} aria-hidden />
            </RadixDialog.Close>
          </RadixDialog.Content>
        </RadixDialog.Overlay>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}

export const DialogClose = RadixDialog.Close;

export interface ConfirmDialogProps {
  aberto?: boolean;
  aoMudar?: (aberto: boolean) => void;
  gatilho?: ReactElement;
  titulo: ReactNode;
  descricao?: ReactNode;
  children?: ReactNode;
  rotuloConfirmar: string;
  /** Gerúndio mostrado enquanto confirma ("Cancelando…"). */
  rotuloConfirmando?: string;
  rotuloVoltar?: string;
  tom?: "perigo" | "primario";
  /** Pode ser assíncrona: o botão fica carregando e o diálogo fecha quando ela resolve. */
  aoConfirmar: () => void | Promise<void>;
  confirmarDesabilitado?: boolean;
  largura?: LarguraDialog;
}

/**
 * Confirmação com foco inicial na ação SEGURA ("Voltar"), nunca na destrutiva (DESIGN.md §4.17).
 * Se aoConfirmar rejeitar, o diálogo continua aberto para quem chamou mostrar o erro.
 */
export function ConfirmDialog({
  aberto,
  aoMudar,
  gatilho,
  titulo,
  descricao,
  children,
  rotuloConfirmar,
  rotuloConfirmando,
  rotuloVoltar = "Voltar",
  tom = "perigo",
  aoConfirmar,
  confirmarDesabilitado = false,
  largura = "confirmacao",
}: ConfirmDialogProps) {
  const [abertoInterno, setAbertoInterno] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const voltarRef = useRef<HTMLButtonElement>(null);
  const estaAberto = aberto ?? abertoInterno;

  function mudar(novo: boolean) {
    if (confirmando) return;
    setAbertoInterno(novo);
    aoMudar?.(novo);
  }

  async function confirmar() {
    setConfirmando(true);
    try {
      await aoConfirmar();
      setConfirmando(false);
      mudar(false);
    } catch {
      // O erro é responsabilidade de quem chamou (toast + mensagem); aqui só destrava o botão.
      setConfirmando(false);
    }
  }

  return (
    <Dialog
      aberto={estaAberto}
      aoMudar={mudar}
      {...(gatilho ? { gatilho } : {})}
      titulo={titulo}
      descricao={descricao}
      largura={largura}
      aoAbrirFoco={(evento) => {
        evento.preventDefault();
        voltarRef.current?.focus();
      }}
      rodape={
        <>
          <Button
            ref={voltarRef}
            variante="secundario"
            desabilitado={confirmando}
            onClick={() => mudar(false)}
          >
            {rotuloVoltar}
          </Button>
          <Button
            variante={tom === "perigo" ? "perigo" : "primario"}
            carregando={confirmando}
            {...(rotuloConfirmando ? { rotuloCarregando: rotuloConfirmando } : {})}
            desabilitado={confirmarDesabilitado}
            onClick={() => void confirmar()}
          >
            {rotuloConfirmar}
          </Button>
        </>
      }
    >
      {children}
    </Dialog>
  );
}

interface DrawerProps {
  aberto: boolean;
  aoMudar: (aberto: boolean) => void;
  /** Título acessível (pode ser só para leitor de tela). */
  titulo: string;
  /** Nome do botão de fechar (alvo de 44 px no topo, também para leitor de tela sem Esc). */
  rotuloFechar?: string;
  children: ReactNode;
  className?: string;
}

/** Foco inicial no item ativo da navegação (o usuário está "ali"); sem ele, o padrão do Radix. */
function focarItemAtivo(evento: Event) {
  const conteudo = evento.currentTarget;
  if (!(conteudo instanceof HTMLElement)) return;
  const ativo = conteudo.querySelector<HTMLElement>('[aria-current="page"]');
  if (!ativo) return;
  evento.preventDefault();
  ativo.focus();
}

/**
 * Gaveta marinho pela esquerda: 300 px (máx. 85vw), foco preso, Esc/overlay/botão Fechar fecham
 * e o foco volta a quem abriu (DESIGN.md §5.2).
 */
export function Drawer({
  aberto,
  aoMudar,
  titulo,
  rotuloFechar = "Fechar menu",
  children,
  className,
}: DrawerProps) {
  const container = useContainerDePortal();
  const foco = useDevolucaoDeFoco();
  return (
    <RadixDialog.Root open={aberto} onOpenChange={aoMudar}>
      <RadixDialog.Portal container={container}>
        <RadixDialog.Overlay className="fixed inset-0 z-50 anima-overlay bg-overlay" />
        <RadixDialog.Content
          aria-describedby={undefined}
          onOpenAutoFocus={(evento) => {
            foco.aoAbrir();
            focarItemAtivo(evento);
          }}
          onCloseAutoFocus={foco.aoFechar}
          className={cn(
            "on-navy fixed inset-y-0 left-0 z-50 flex w-[300px] max-w-[85vw] anima-drawer flex-col overflow-y-auto bg-sidebar shadow-modal outline-none",
            className,
          )}
        >
          <RadixDialog.Title className="sr-only">{titulo}</RadixDialog.Title>
          <RadixDialog.Close
            aria-label={rotuloFechar}
            className="absolute top-[18px] right-2 z-10 grid size-11 place-items-center rounded-md text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy"
          >
            <X size={18} aria-hidden />
          </RadixDialog.Close>
          {children}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}

import { LoaderCircle, type LucideIcon } from "lucide-react";
import { Slot } from "radix-ui";
import {
  useLayoutEffect,
  useRef,
  type ComponentProps,
  type MouseEvent,
  type ReactNode,
  type Ref,
} from "react";

import { cn } from "../utils/cn";
import { buttonClasses, type TamanhoBotao, type VarianteBotao } from "./botao";

export type { TamanhoBotao, VarianteBotao } from "./botao";

interface BotaoBaseProps {
  variante?: VarianteBotao;
  tamanho?: TamanhoBotao;
  /** Ícone à esquerda (18 px). Durante o carregamento vira spinner. */
  icone?: LucideIcon;
  iconeDireita?: LucideIcon;
  /** Bloqueia o clique, troca o ícone por spinner e anuncia aria-busy. */
  carregando?: boolean;
  /** Rótulo no gerúndio durante o carregamento ("Emitindo…"). */
  rotuloCarregando?: string;
  larguraTotal?: boolean;
}

export type ButtonProps = BotaoBaseProps &
  Omit<ComponentProps<"button">, "disabled"> & {
    /** Desabilita sem tirar o foco do botão (aria-disabled), para o leitor de tela achá-lo. */
    desabilitado?: boolean;
    /**
     * Renderiza o filho (ex.: Link) com o visual do botão. Ícones, carregando, desabilitado,
     * onClick e ref continuam valendo (os ícones entram dentro do filho).
     */
    asChild?: boolean;
  };

function atribuirRef<T>(ref: Ref<T> | undefined, valor: T | null) {
  if (typeof ref === "function") ref(valor);
  else if (ref) ref.current = valor;
}

/**
 * Largura travada durante o carregamento: mede o botão ENQUANTO ele ainda mostra o rótulo normal
 * (a medição depois do commit já veria o gerúndio) e fixa essa largura enquanto carrega.
 */
function useLarguraTravada(carregando: boolean) {
  const elementoRef = useRef<HTMLElement | null>(null);
  const larguraEmRepousoRef = useRef<number | null>(null);

  useLayoutEffect(() => {
    const el = elementoRef.current;
    if (!el) return;
    if (!carregando) {
      el.style.removeProperty("width");
      larguraEmRepousoRef.current = el.getBoundingClientRect().width;
      return;
    }
    const largura = larguraEmRepousoRef.current;
    if (largura) el.style.width = `${String(largura)}px`;
  });

  return elementoRef;
}

export function Button({
  variante = "primario",
  tamanho = "md",
  icone: Icone,
  iconeDireita: IconeDireita,
  carregando = false,
  rotuloCarregando,
  larguraTotal = false,
  desabilitado = false,
  asChild = false,
  type = "button",
  className,
  children,
  onClick,
  ref,
  ...resto
}: ButtonProps) {
  // Botão de largura total já tem largura estável: não trava (acompanha o redimensionamento).
  const elementoRef = useLarguraTravada(carregando && !larguraTotal);
  const bloqueado = desabilitado || carregando;

  function aoClicar(evento: MouseEvent<HTMLElement>) {
    if (bloqueado) {
      // Também segura o submit implícito (Enter no formulário dispara um clique aqui) e, com
      // asChild, a navegação do link.
      evento.preventDefault();
      return;
    }
    // O evento vem do <button> (ou, com asChild, do filho que faz o papel dele).
    onClick?.(evento as MouseEvent<HTMLButtonElement>);
  }

  function juntarRefs(el: HTMLElement | null) {
    elementoRef.current = el;
    // Com asChild o elemento é o filho (ex.: <a>); quem passa ref para um Button asChild recebe-o.
    atribuirRef(ref, el as HTMLButtonElement | null);
  }

  const classes = cn(buttonClasses({ variante, tamanho, larguraTotal }), className);
  const rotulo = carregando ? (rotuloCarregando ?? children) : children;
  const iconeEsquerda = carregando ? (
    <LoaderCircle
      size={16}
      className="motion-safe:animate-[girar_800ms_linear_infinite]"
      aria-hidden
    />
  ) : (
    Icone && <Icone size={18} aria-hidden />
  );
  const iconeFinal = !carregando && IconeDireita && <IconeDireita size={18} aria-hidden />;
  const estado = {
    "aria-disabled": bloqueado || undefined,
    "aria-busy": carregando || undefined,
  };

  if (asChild) {
    // Slottable injeta os ícones dentro do filho, em volta do conteúdo dele.
    return (
      <Slot.Root ref={juntarRefs} className={classes} onClick={aoClicar} {...estado} {...resto}>
        {iconeEsquerda}
        <Slot.Slottable>{children}</Slot.Slottable>
        {iconeFinal}
      </Slot.Root>
    );
  }

  return (
    <button
      ref={juntarRefs}
      type={type}
      className={classes}
      onClick={aoClicar}
      {...estado}
      {...resto}
    >
      {iconeEsquerda}
      {/* Rótulo trunca em vez de vazar se o gerúndio for maior que a largura travada. */}
      <ConteudoRotulo>{rotulo}</ConteudoRotulo>
      {iconeFinal}
    </button>
  );
}

function ConteudoRotulo({ children }: { children: ReactNode }) {
  return <span className="min-w-0 truncate">{children}</span>;
}

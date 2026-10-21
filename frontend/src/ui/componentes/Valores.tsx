import type { ComponentProps } from "react";

import { cn } from "../utils/cn";
import {
  formatarData,
  formatarDataHora,
  formatarMoeda,
  formatarPercentual,
  formatarRelativo,
  partesMoeda,
} from "../utils/formatar";

interface MoneyProps extends Omit<ComponentProps<"span">, "children"> {
  valor: number;
  /**
   * "padrao": R$ 4.528,20 em sans tabular (tabela, lista, formulário).
   * "heroi": R$ sobrescrito e centavos reduzidos, em serifa. Só até 2 por tela (DESIGN.md §3.8).
   */
  variante?: "padrao" | "heroi";
  /** Sobre marinho o "R$" do herói usa --on-navy-2. */
  sobreMarinho?: boolean;
  /** Mostra sinal de + em valores positivos (ajustes, efeitos de fator). */
  comSinal?: boolean;
}

export function Money({
  valor,
  variante = "padrao",
  sobreMarinho = false,
  comSinal = false,
  className,
  ...resto
}: MoneyProps) {
  // Sinal tipográfico (− U+2212) em vez do hífen do Intl; + só quando pedido (ajustes, efeitos).
  const prefixoSinal = valor < 0 ? "−" : comSinal && valor > 0 ? "+" : "";
  const texto = `${prefixoSinal}${formatarMoeda(Math.abs(valor))}`;
  if (variante === "padrao") {
    return (
      <span className={cn("whitespace-nowrap tnum", className)} {...resto}>
        {texto}
      </span>
    );
  }
  const { sinal, simbolo, inteiro, centavos } = partesMoeda(valor);
  return (
    <span className={cn("fig whitespace-nowrap", className)} {...resto}>
      {/* O leitor de tela ouve o valor inteiro; as partes visuais ficam ocultas. */}
      <span className="sr-only">{texto}</span>
      <span aria-hidden>
        <span
          className={cn(
            "mr-[0.28em] align-[0.55em] font-sans text-[max(11px,0.42em)] font-medium tracking-normal",
            sobreMarinho ? "text-on-navy-2" : "text-ink-2",
          )}
        >
          {simbolo}
        </span>
        {sinal}
        {inteiro}
        <span className="text-[0.6em] tracking-normal">{centavos}</span>
      </span>
    </span>
  );
}

interface DateTextProps extends Omit<ComponentProps<"time">, "children" | "dateTime"> {
  /** DateOnly ("2026-10-21"), instante ISO ou Date. */
  valor: string | Date;
  formato?: "data" | "data-hora" | "relativo";
  /** Referência para o tempo relativo (testes e telas congeladas). */
  agora?: Date;
}

export function DateText({ valor, formato = "data", agora, className, ...resto }: DateTextProps) {
  const iso = valor instanceof Date ? valor.toISOString() : valor;
  if (formato === "relativo") {
    const absoluto = formatarDataHora(valor);
    return (
      <time dateTime={iso} title={absoluto} className={cn("tnum", className)} {...resto}>
        {formatarRelativo(valor, agora)}
      </time>
    );
  }
  return (
    <time dateTime={iso} className={cn("whitespace-nowrap tnum", className)} {...resto}>
      {formato === "data" ? formatarData(valor) : formatarDataHora(valor)}
    </time>
  );
}

interface PercentProps extends Omit<ComponentProps<"span">, "children"> {
  /** Fração: 0,0738 → 7,38%. */
  valor: number;
  casas?: number;
}

export function Percent({ valor, casas = 2, className, ...resto }: PercentProps) {
  return (
    <span className={cn("whitespace-nowrap tnum", className)} {...resto}>
      {formatarPercentual(valor, casas)}
    </span>
  );
}

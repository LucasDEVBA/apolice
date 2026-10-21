import { useState, type FocusEvent } from "react";

import { formatarNumero, lerMoedaDigitada } from "../utils/formatar";
import { Input, type InputProps } from "./Input";

const CASAS_CENTAVOS = 2;

function formatarParaCampo(valor: number | undefined): string {
  if (valor === undefined || Number.isNaN(valor)) return "";
  return formatarNumero(valor, CASAS_CENTAVOS);
}

export interface InputMoedaProps extends Omit<
  InputProps,
  "value" | "defaultValue" | "onChange" | "type" | "inputMode"
> {
  valor: number | undefined;
  aoMudar: (valor: number | undefined) => void;
}

/**
 * Campo de moeda com máscara pt-BR (DESIGN.md §4.8): enquanto edita, aceita o que a pessoa digita
 * (só algarismos, ponto e vírgula); fora do foco mostra 100.000,00. Use com prefixo "R$"/"LMI R$".
 */
export function InputMoeda({ valor, aoMudar, onFocus, onBlur, ...resto }: InputMoedaProps) {
  // null = fora de edição: o campo mostra o valor formatado.
  const [texto, setTexto] = useState<string | null>(null);

  return (
    <Input
      {...resto}
      inputMode="decimal"
      autoComplete="off"
      value={texto ?? formatarParaCampo(valor)}
      onFocus={(evento: FocusEvent<HTMLInputElement>) => {
        setTexto(formatarParaCampo(valor));
        onFocus?.(evento);
      }}
      onChange={(evento) => {
        const digitado = evento.target.value.replace(/[^\d.,]/g, "");
        setTexto(digitado);
        aoMudar(lerMoedaDigitada(digitado));
      }}
      onBlur={(evento: FocusEvent<HTMLInputElement>) => {
        setTexto(null);
        onBlur?.(evento);
      }}
    />
  );
}

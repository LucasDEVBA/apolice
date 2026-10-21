import { CircleX } from "lucide-react";
import { useId, type ReactNode } from "react";

import { cn } from "../utils/cn";

/** Props de acessibilidade que o FormField entrega ao controle. */
export interface PropsDeCampo {
  id: string;
  "aria-labelledby": string;
  "aria-describedby"?: string;
  "aria-invalid"?: true;
  "aria-required"?: true;
}

interface FormFieldProps {
  rotulo: string;
  /** Ajuda abaixo do campo: faixa válida, regra explicada na voz do negócio. */
  ajuda?: ReactNode;
  /** Mensagem de erro já em pt-BR, dizendo o que fazer. */
  erro?: string | undefined;
  obrigatorio?: boolean;
  /** Ação à direita do rótulo (ex.: "Esqueci minha senha"). */
  acao?: ReactNode;
  /** À direita da ajuda, na mesma linha (ex.: <ContadorCaracteres />); entra na descrição. */
  contador?: ReactNode;
  className?: string;
  children: (campo: PropsDeCampo) => ReactNode;
}

/**
 * Rótulo + controle + ajuda + erro, ligados por id/aria-describedby. O controle chega por
 * render prop para funcionar igual com Input, Select (Radix), Switch e SegmentedControl.
 */
export function FormField({
  rotulo,
  ajuda,
  erro,
  obrigatorio = false,
  acao,
  contador,
  className,
  children,
}: FormFieldProps) {
  const base = useId();
  const ids = {
    campo: `${base}-campo`,
    rotulo: `${base}-rotulo`,
    ajuda: `${base}-ajuda`,
    contador: `${base}-contador`,
    erro: `${base}-erro`,
  };
  const descritores = [
    ajuda ? ids.ajuda : null,
    contador ? ids.contador : null,
    erro ? ids.erro : null,
  ]
    .filter(Boolean)
    .join(" ");

  const campo: PropsDeCampo = {
    id: ids.campo,
    "aria-labelledby": ids.rotulo,
    ...(descritores ? { "aria-describedby": descritores } : {}),
    ...(erro ? { "aria-invalid": true as const } : {}),
    ...(obrigatorio ? { "aria-required": true as const } : {}),
  };

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <div className="flex items-baseline justify-between gap-3">
        <label id={ids.rotulo} htmlFor={ids.campo} className="type-label text-ink">
          {rotulo}
          {obrigatorio && (
            <span className="text-ink-3" aria-hidden>
              {" "}
              *
            </span>
          )}
        </label>
        {acao}
      </div>
      {children(campo)}
      {(ajuda ?? contador) && (
        <div className="flex items-baseline justify-between gap-3">
          {ajuda && (
            <p id={ids.ajuda} className="type-caption font-normal text-ink-3 tnum">
              {ajuda}
            </p>
          )}
          {contador && (
            <span id={ids.contador} className="ml-auto shrink-0 type-caption">
              {contador}
            </span>
          )}
        </div>
      )}
      {erro && (
        <p id={ids.erro} className="flex items-start gap-1.5 type-meta text-bad">
          <CircleX size={15} className="mt-px shrink-0" aria-hidden />
          {erro}
        </p>
      )}
    </div>
  );
}

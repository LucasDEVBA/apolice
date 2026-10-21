import {
  Check,
  CircleCheck,
  CircleX,
  Copy,
  Info,
  TriangleAlert,
  X,
  type LucideIcon,
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { cn } from "../utils/cn";

export type TipoToast = "sucesso" | "info" | "alerta" | "erro";

const ICONES: Record<TipoToast, { icone: LucideIcon; cor: string }> = {
  sucesso: { icone: CircleCheck, cor: "text-ok-on-navy" },
  info: { icone: Info, cor: "text-info-on-navy" },
  alerta: { icone: TriangleAlert, cor: "text-warn-on-navy" },
  erro: { icone: CircleX, cor: "text-bad-on-navy" },
};

const MS_CONFIRMACAO_COPIA = 2_000;
const ACAO =
  "rounded-xs text-label font-semibold text-gold-on-navy hover:underline hover:underline-offset-[3px]";

export interface AcaoToast {
  rotulo: string;
  aoClicar: () => void;
}

export interface OpcoesToast {
  titulo: string;
  descricao?: string;
  acao?: AcaoToast;
  /** Erros mostram o ID de correlação copiável (ProblemDetails). */
  correlationId?: string;
}

export interface CartaoToastProps extends OpcoesToast {
  id: string | number;
  tipo: TipoToast;
}

/** "Copiar ID" com retorno visível e anunciado; sem área de transferência, o ID segue legível. */
function CopiarId({ correlationId }: { correlationId: string }) {
  const [copiado, setCopiado] = useState(false);

  async function copiar() {
    // Em contexto inseguro (http fora de localhost) navigator.clipboard nem existe.
    const areaDeTransferencia = navigator.clipboard as Clipboard | undefined;
    if (!areaDeTransferencia) return;
    try {
      await areaDeTransferencia.writeText(correlationId);
      setCopiado(true);
      window.setTimeout(() => setCopiado(false), MS_CONFIRMACAO_COPIA);
    } catch {
      // Permissão negada: o ID continua visível e selecionável no próprio toast.
      setCopiado(false);
    }
  }

  return (
    <>
      <button
        type="button"
        onClick={() => void copiar()}
        className={cn(ACAO, "mt-1.5 inline-flex items-center gap-1.5")}
      >
        {copiado ? <Check size={13} aria-hidden /> : <Copy size={13} aria-hidden />}
        <span>
          {copiado ? "ID copiado" : "Copiar ID"}{" "}
          <span className="mono-id font-medium text-on-navy-2 select-all">{correlationId}</span>
        </span>
      </button>
      <span className="sr-only" aria-live="polite">
        {copiado ? "ID de correlação copiado" : ""}
      </span>
    </>
  );
}

export function CartaoToast({
  id,
  tipo,
  titulo,
  descricao,
  acao,
  correlationId,
}: CartaoToastProps) {
  const { icone: Icone, cor } = ICONES[tipo];
  return (
    <div className="on-navy flex w-full gap-3 rounded-lg border border-toast-line bg-toast p-4 text-on-navy shadow-overlay">
      <Icone size={18} className={cn("mt-px shrink-0", cor)} aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold">{titulo}</p>
        {descricao && <p className="mt-0.5 text-label text-on-navy-2">{descricao}</p>}
        {correlationId && <CopiarId correlationId={correlationId} />}
        {acao && (
          <button
            type="button"
            onClick={() => {
              acao.aoClicar();
              toast.dismiss(id);
            }}
            className={cn(ACAO, "mt-2 block")}
          >
            {acao.rotulo}
          </button>
        )}
      </div>
      <button
        type="button"
        aria-label="Fechar notificação"
        onClick={() => toast.dismiss(id)}
        className="-mt-2.5 -mr-2.5 grid size-11 shrink-0 place-items-center rounded-md text-on-navy-3 hover:bg-hover-tint-navy hover:text-on-navy md:size-10"
      >
        <X size={15} aria-hidden />
      </button>
    </div>
  );
}

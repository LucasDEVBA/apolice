import { Check, Copy, OctagonAlert, RotateCw } from "lucide-react";
import { useState } from "react";

import { Button } from "./Button";
import { EmptyState } from "./EmptyState";

interface ErrorStateProps {
  /** "Não foi possível carregar as apólices". */
  titulo: string;
  descricao?: string;
  /** correlationId do ProblemDetails (SPEC §7), para o suporte achar o log. */
  correlationId?: string | undefined;
  aoTentarDeNovo?: () => void;
  nivel?: "h1" | "h2" | "h3";
  className?: string;
}

const MS_CONFIRMACAO_COPIA = 2_000;

/** Variante de erro do estado vazio: tentar de novo + ID de correlação copiável (DESIGN.md §4.16). */
export function ErrorState({
  titulo,
  descricao = "Verifique a conexão e tente de novo. Se continuar, informe o ID abaixo ao suporte.",
  correlationId,
  aoTentarDeNovo,
  nivel = "h2",
  className,
}: ErrorStateProps) {
  const [copiado, setCopiado] = useState(false);

  async function copiar() {
    // Em contexto inseguro (http fora de localhost) navigator.clipboard nem existe.
    const areaDeTransferencia = navigator.clipboard as Clipboard | undefined;
    if (!correlationId || !areaDeTransferencia) return;
    try {
      await areaDeTransferencia.writeText(correlationId);
      setCopiado(true);
      window.setTimeout(() => setCopiado(false), MS_CONFIRMACAO_COPIA);
    } catch {
      // Sem permissão de área de transferência: o ID continua visível e selecionável.
      setCopiado(false);
    }
  }

  return (
    <EmptyState
      icone={OctagonAlert}
      titulo={titulo}
      descricao={descricao}
      nivel={nivel}
      {...(className ? { className } : {})}
      {...(aoTentarDeNovo
        ? {
            acao: (
              <Button variante="primario" icone={RotateCw} onClick={aoTentarDeNovo}>
                Tentar de novo
              </Button>
            ),
          }
        : {})}
    >
      {correlationId && (
        <div className="mt-4 flex items-center gap-2 rounded-md border border-line bg-surface-2 py-1 pr-1 pl-3 text-label text-ink-2">
          <span>ID de correlação</span>
          <code className="mono-id text-ink select-all">{correlationId}</code>
          <button
            type="button"
            onClick={() => void copiar()}
            className="grid size-11 place-items-center rounded-[6px] text-ink-2 hover:bg-hover-tint hover:text-ink md:size-10"
            aria-label={copiado ? "ID copiado" : "Copiar ID de correlação"}
          >
            {copiado ? (
              <Check size={15} className="text-ok" aria-hidden />
            ) : (
              <Copy size={15} aria-hidden />
            )}
          </button>
          <span className="sr-only" aria-live="polite">
            {copiado ? "ID de correlação copiado" : ""}
          </span>
        </div>
      )}
    </EmptyState>
  );
}

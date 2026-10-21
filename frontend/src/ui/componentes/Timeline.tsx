import type { ReactNode } from "react";

import { cn } from "../utils/cn";
import {
  formatarData,
  formatarDataHora,
  formatarRelativo,
  lerDataSomente,
} from "../utils/formatar";

/** Tom do nó: sinistro warn, endosso dourado, notificação aço, emissão marinho, cancelamento bad. */
export type TomEvento = "warn" | "gold" | "steel" | "navy" | "bad" | "ok";

const NO: Record<TomEvento, { fundo: string; ponto: string }> = {
  warn: { fundo: "bg-warn-bg", ponto: "bg-warn" },
  gold: { fundo: "bg-tl-gold-bg", ponto: "bg-tl-gold" },
  steel: { fundo: "bg-tl-steel-bg", ponto: "bg-tl-steel" },
  navy: { fundo: "bg-tl-navy-bg", ponto: "bg-tl-navy" },
  bad: { fundo: "bg-bad-bg", ponto: "bg-bad" },
  ok: { fundo: "bg-ok-bg", ponto: "bg-ok" },
};

export interface EventoTimeline {
  id: string;
  /** Instante ISO ("2026-10-21T11:47:00-03:00") ou DateOnly ("2026-11-15"). */
  quando: string;
  titulo: ReactNode;
  descricao?: ReactNode;
  tom: TomEvento;
  /** Badge opcional abaixo da descrição. */
  extra?: ReactNode;
}

interface TimelineProps {
  eventos: readonly EventoTimeline[];
  /** Com data de hoje, insere o marcador "Hoje" entre futuro e passado (DESIGN.md §4.14). */
  hoje?: Date;
  rotulo?: string;
  className?: string;
}

function instante(quando: string): number {
  if (/^\d{4}-\d{2}-\d{2}$/.test(quando)) {
    const { ano, mes, dia } = lerDataSomente(quando);
    return new Date(ano, mes - 1, dia).getTime();
  }
  return new Date(quando).getTime();
}

function Quando({ quando, hoje }: { quando: string; hoje?: Date | undefined }) {
  const soData = /^\d{4}-\d{2}-\d{2}$/.test(quando);
  return (
    <time
      dateTime={quando}
      title={soData ? undefined : formatarRelativo(quando, hoje)}
      className="type-meta text-ink-3 tnum"
    >
      {soData ? formatarData(quando) : formatarDataHora(quando)}
    </time>
  );
}

/** Linha do tempo: mais recente no topo, nós de 20 px ligados por fio de 1 px. */
export function Timeline({ eventos, hoje, rotulo = "Linha do tempo", className }: TimelineProps) {
  const ordenados = [...eventos].sort((a, b) => instante(b.quando) - instante(a.quando));
  const agora = hoje?.getTime();
  const temFuturo = agora !== undefined && ordenados.some((e) => instante(e.quando) > agora);
  const temPassado = agora !== undefined && ordenados.some((e) => instante(e.quando) <= agora);
  const indiceHoje =
    temFuturo && temPassado ? ordenados.findIndex((e) => instante(e.quando) <= agora) : -1;

  return (
    <ol aria-label={rotulo} className={cn("flex flex-col", className)}>
      {ordenados.map((evento, indice) => {
        const no = NO[evento.tom];
        return (
          <li key={evento.id}>
            {indice === indiceHoje && (
              <div className="mb-4 flex items-center gap-3 pl-8">
                <span className="type-caption font-semibold text-gold-text">Hoje</span>
                <span aria-hidden className="h-px flex-1 bg-gold-line/50" />
              </div>
            )}
            <div
              className={cn(
                "relative grid grid-cols-[20px_1fr] gap-3 pb-5",
                "before:absolute before:top-[22px] before:bottom-0.5 before:left-[9.5px] before:w-px before:bg-line",
                indice === ordenados.length - 1 && "pb-0 before:hidden",
              )}
            >
              <span
                aria-hidden
                className={cn("mt-px grid size-5 place-items-center rounded-full", no.fundo)}
              >
                <span className={cn("size-2 rounded-full", no.ponto)} />
              </span>
              <div className="min-w-0">
                <Quando quando={evento.quando} hoje={hoje} />
                <p className="mt-0.5 text-sm font-semibold text-ink">{evento.titulo}</p>
                {evento.descricao && (
                  <div className="mt-[3px] text-label leading-normal text-ink-2">
                    {evento.descricao}
                  </div>
                )}
                {evento.extra && <div className="mt-2">{evento.extra}</div>}
              </div>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

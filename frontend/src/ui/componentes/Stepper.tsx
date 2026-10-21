import { Check, ChevronDown } from "lucide-react";
import { Link } from "react-router";

import { cn } from "../utils/cn";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "./DropdownMenu";

export interface EtapaStepper {
  id: string;
  titulo: string;
  /** Resumo do que foi preenchido ("Lucas Andrade · 22 anos"). */
  resumo?: string;
  /** Destino para voltar e editar (só etapas concluídas são navegáveis). */
  href?: string;
}

interface StepperProps {
  etapas: readonly EtapaStepper[];
  /** Índice (0-based) da etapa em edição. */
  atual: number;
  rotulo?: string;
  className?: string;
}

type Situacao = "concluida" | "atual" | "futura";

function situacaoDa(indice: number, atual: number): Situacao {
  if (indice < atual) return "concluida";
  return indice === atual ? "atual" : "futura";
}

const BARRA: Record<Situacao, string> = {
  concluida: "border-chart-1",
  atual: "border-gold-line",
  futura: "border-line",
};

const CIRCULO: Record<Situacao, string> = {
  concluida: "bg-primary text-primary-fg",
  atual: "bg-surface text-ink ring-2 ring-primary ring-inset",
  futura: "bg-surface text-ink-3 ring-1 ring-control ring-inset",
};

function ConteudoEtapa({
  etapa,
  indice,
  situacao,
}: {
  etapa: EtapaStepper;
  indice: number;
  situacao: Situacao;
}) {
  return (
    <>
      <span
        aria-hidden
        className={cn(
          "grid size-7 shrink-0 place-items-center rounded-full text-label font-bold tnum",
          CIRCULO[situacao],
        )}
      >
        {situacao === "concluida" ? <Check size={15} strokeWidth={2.4} /> : indice + 1}
      </span>
      <span className="mt-[3px] min-w-0">
        <span className="block text-sm leading-tight font-semibold text-ink">{etapa.titulo}</span>
        {situacao === "atual" ? (
          <span className="block type-meta font-semibold text-gold-text">Em edição</span>
        ) : (
          etapa.resumo && (
            <span className="block truncate type-meta text-ink-3">{etapa.resumo}</span>
          )
        )}
      </span>
      <span className="sr-only">
        {situacao === "concluida" ? " (concluída)" : situacao === "futura" ? " (pendente)" : ""}
      </span>
    </>
  );
}

function ItemEtapa({
  etapa,
  indice,
  situacao,
}: {
  etapa: EtapaStepper;
  indice: number;
  situacao: Situacao;
}) {
  const classes = cn("-mt-px flex items-start gap-3 border-t-[3px] pt-[18px]", BARRA[situacao]);
  const conteudo = <ConteudoEtapa etapa={etapa} indice={indice} situacao={situacao} />;
  return (
    <li aria-current={situacao === "atual" ? "step" : undefined} className="min-w-0">
      {situacao === "concluida" && etapa.href ? (
        <Link to={etapa.href} className={cn(classes, "rounded-b-md hover:[&_span]:text-ink")}>
          {conteudo}
        </Link>
      ) : (
        <div className={classes}>{conteudo}</div>
      )}
    </li>
  );
}

/** Lista completa das etapas no menu "Ver etapas" do mobile. */
function MenuEtapas({ etapas, atual }: { etapas: readonly EtapaStepper[]; atual: number }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="inline-flex h-11 items-center gap-1 rounded-md px-2 text-label font-semibold text-link">
        Ver etapas
        <ChevronDown size={15} aria-hidden />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {etapas.map((etapa, indice) => {
          const situacao = situacaoDa(indice, atual);
          const rotuloItem = `${String(indice + 1)}. ${etapa.titulo}`;
          return situacao === "concluida" && etapa.href ? (
            <DropdownMenuItem key={etapa.id} asChild icone={Check}>
              <Link to={etapa.href}>
                {rotuloItem}
                <span className="sr-only"> (concluída)</span>
              </Link>
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem key={etapa.id} disabled={situacao === "futura"}>
              {rotuloItem}
              {situacao === "atual" && (
                <span className="ml-auto type-meta text-gold-text">Em edição</span>
              )}
              {situacao === "futura" && <span className="sr-only"> (pendente)</span>}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Mobile: "Etapa 4 de 5 · Coberturas" + barra segmentada; a lista completa abre num menu. */
function StepperCompacto({ etapas, atual }: { etapas: readonly EtapaStepper[]; atual: number }) {
  const etapaAtual = etapas[atual];
  return (
    <div className="flex flex-col gap-3 px-4 py-3.5 md:hidden">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-ink-2" aria-live="polite">
          <span className="font-semibold text-ink tnum">
            Etapa {atual + 1} de {etapas.length}
          </span>
          {etapaAtual && <> · {etapaAtual.titulo}</>}
        </p>
        <MenuEtapas etapas={etapas} atual={atual} />
      </div>
      <div
        aria-hidden
        className="grid gap-1"
        style={{ gridTemplateColumns: `repeat(${String(etapas.length)}, 1fr)` }}
      >
        {etapas.map((etapa, indice) => (
          <span
            key={etapa.id}
            className={cn("h-[3px] rounded-full border-t-[3px]", BARRA[situacaoDa(indice, atual)])}
          />
        ))}
      </div>
    </div>
  );
}

/**
 * Assistente em 5 etapas (DESIGN.md §4.11): barra superior de 3 px por estado, círculo de 28 px,
 * etapa atual com aria-current="step". No mobile vira "Etapa 4 de 5 · Coberturas" + barra segmentada.
 */
export function Stepper({ etapas, atual, rotulo = "Etapas da cotação", className }: StepperProps) {
  return (
    <nav aria-label={rotulo} className={cn("rounded-xl border border-line bg-surface", className)}>
      <ol className="hidden grid-cols-5 gap-3 px-6 pb-[18px] md:grid">
        {etapas.map((etapa, indice) => (
          <ItemEtapa
            key={etapa.id}
            etapa={etapa}
            indice={indice}
            situacao={situacaoDa(indice, atual)}
          />
        ))}
      </ol>
      <StepperCompacto etapas={etapas} atual={atual} />
    </nav>
  );
}

interface StatusRascunhoProps {
  /** Hora do último salvamento (ex.: "14:36"); vazio = ainda não salvo. */
  salvoAs?: string | undefined;
  salvando?: boolean;
}

/** "✓ Rascunho salvo às 14:36" — discreto e anunciado com aria-live polite. */
export function StatusRascunho({ salvoAs, salvando = false }: StatusRascunhoProps) {
  return (
    <p aria-live="polite" className="inline-flex items-center gap-1.5 text-label text-ink-3 tnum">
      {salvando ? (
        "Salvando rascunho…"
      ) : salvoAs ? (
        <>
          <Check size={15} className="text-ok" aria-hidden />
          Rascunho salvo às {salvoAs}
        </>
      ) : null}
    </p>
  );
}

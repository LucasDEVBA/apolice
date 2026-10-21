import { ArrowDown, ArrowUp } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { cn } from "../utils/cn";
import {
  formatarCompacto,
  formatarMoeda,
  formatarNumero,
  formatarPercentual,
} from "../utils/formatar";

const MIL = 1_000;

export type ValorKpi =
  | { tipo: "inteiro"; valor: number }
  | { tipo: "moeda"; valor: number }
  | { tipo: "percentual"; valor: number; casas?: number };

interface PartesKpi {
  prefixo?: string;
  numero: string;
  sufixo?: string;
  completo: string;
  /** Valor abreviado (mil/mi): o completo vai no title e no texto do leitor de tela. */
  abreviado?: boolean;
}

function partesDoKpi(valor: ValorKpi): PartesKpi {
  switch (valor.tipo) {
    case "inteiro": {
      const numero = formatarNumero(valor.valor);
      return { numero, completo: numero };
    }
    case "percentual": {
      const casas = valor.casas ?? 1;
      return {
        numero: formatarNumero(valor.valor * 100, casas),
        sufixo: "%",
        completo: formatarPercentual(valor.valor, casas),
      };
    }
    case "moeda": {
      const completo = formatarMoeda(valor.valor);
      // Valor pequeno (o seed tem poucas apólices) fica inteiro; abreviado só a partir de mil.
      if (Math.abs(valor.valor) < MIL) {
        return { prefixo: "R$", numero: formatarNumero(Math.round(valor.valor)), completo };
      }
      const { numero, sufixo } = formatarCompacto(valor.valor);
      return { prefixo: "R$", numero, sufixo, completo, abreviado: true };
    }
  }
}

const UNIDADE = "font-sans text-[max(11px,0.42em)] font-medium tracking-normal text-ink-2";

/** kpi 36/28 (faixa de KPIs) · total 28 (total do gráfico) · sm 19 (lista de distribuição). */
const TAMANHO_NUMERO = {
  kpi: "type-fig-kpi",
  total: "type-fig-total",
  sm: "type-fig-sm",
} as const;

interface NumeroKpiProps {
  valor: ValorKpi;
  tamanho?: keyof typeof TAMANHO_NUMERO;
  className?: string;
}

/**
 * Valor em serifa no formato de KPI: R$ e mil/mi a 0,42em NA LINHA DE BASE (nunca sobrescrito,
 * que é só do número-herói). Abreviado leva o valor completo no title e no leitor de tela.
 */
export function NumeroKpi({ valor, tamanho = "kpi", className }: NumeroKpiProps) {
  const { prefixo, numero, sufixo, completo, abreviado = false } = partesDoKpi(valor);
  return (
    <span
      className={cn("fig whitespace-nowrap text-ink", TAMANHO_NUMERO[tamanho], className)}
      title={abreviado ? completo : undefined}
    >
      <span className="sr-only">{completo}</span>
      <span aria-hidden>
        {prefixo && <span className={cn(UNIDADE, "mr-[0.28em]")}>{prefixo}</span>}
        {numero}
        {sufixo && <span className={cn(UNIDADE, "ml-[0.22em]")}>{sufixo}</span>}
      </span>
    </span>
  );
}

interface DeltaProps {
  /** Variação: positiva sobe, negativa desce. */
  valor: number;
  formato?: "inteiro" | "percentual";
  /** Para sinistralidade, subir é ruim. */
  sentido?: "maior-melhor" | "menor-melhor";
  children?: ReactNode;
}

/** Delta sempre com seta + sinal, nunca só cor (DESIGN.md §2.6). */
export function Delta({
  valor,
  formato = "inteiro",
  sentido = "maior-melhor",
  children,
}: DeltaProps) {
  const subiu = valor >= 0;
  const bom = sentido === "maior-melhor" ? subiu : !subiu;
  const Seta = subiu ? ArrowUp : ArrowDown;
  const numero =
    formato === "percentual"
      ? formatarPercentual(Math.abs(valor), 1)
      : formatarNumero(Math.abs(valor));
  return (
    <span className="tnum">
      <span
        className={cn(
          "mr-1 inline-flex items-center gap-0.5 font-semibold",
          bom ? "text-ok" : "text-bad",
        )}
      >
        <Seta size={13} strokeWidth={2} aria-hidden />
        {subiu ? "+" : "−"}
        {numero}
      </span>
      {/* O comparativo ("vs. 1–21/set") não quebra no meio do intervalo. */}
      {children && <span className="whitespace-nowrap">{children}</span>}
    </span>
  );
}

/** Número de atenção em --warn seguido de texto: "12 expiram em 48 h". */
export function Atencao({ numero, children }: { numero: number; children: ReactNode }) {
  return (
    <span className="tnum">
      <span className="mr-1 font-semibold text-warn">{formatarNumero(numero)}</span>
      {children}
    </span>
  );
}

interface MedidorProps {
  /** Fração (0,542). */
  valor: number;
  /** Fração da meta (0,60). */
  meta: number;
}

/** Trilho 4 px + marcador de meta 2 × 12 em --gold-text; acima da meta o preenchimento vira --warn. */
export function Medidor({ valor, meta }: MedidorProps) {
  const acima = valor > meta;
  const pct = (fracao: number) => `${String(Math.min(100, Math.max(0, fracao * 100)))}%`;
  return (
    <div className="mt-3">
      <div aria-hidden className="relative h-1 rounded-full bg-meter-track">
        <span
          className={cn("absolute inset-y-0 left-0 rounded-full", acima ? "bg-warn" : "bg-link")}
          style={{ width: pct(valor) }}
        />
        <span
          className="absolute -top-1 h-3 w-0.5 rounded-[1px] bg-gold-text"
          style={{ left: pct(meta) }}
        />
      </div>
      <p className="mt-2 type-meta text-ink-3 tnum">
        Meta até {formatarPercentual(meta, 0)}
        {acima && <span className="font-semibold text-warn"> · acima da meta</span>}
      </p>
    </div>
  );
}

export interface KpiProps {
  rotulo: string;
  /** Rótulo do mobile (2 colunas estreitas): "Conversão 90 dias". Sem ele, vale o rótulo. */
  rotuloCurto?: string;
  valor: ValorKpi;
  meta?: ReactNode;
  medidor?: MedidorProps;
  /** Lista filtrada correspondente: o KPI inteiro vira link. */
  para?: string;
}

function RotuloKpi({ rotulo, rotuloCurto }: Pick<KpiProps, "rotulo" | "rotuloCurto">) {
  const classes = "block text-label leading-tight font-medium text-ink-2";
  if (!rotuloCurto) return <span className={classes}>{rotulo}</span>;
  return (
    <>
      <span className={cn(classes, "md:hidden")}>{rotuloCurto}</span>
      <span className={cn(classes, "hidden md:block")}>{rotulo}</span>
    </>
  );
}

/*
 * Grade com fios reais (não sombra: o fundo da célula seguinte cobria a sombra da anterior).
 * Abaixo de xl cada célula desenha a borda direita e a inferior; a faixa recolhe 1 px à direita e
 * embaixo (KpiCard) e o overflow-hidden do card esconde os fios da borda externa. No xl, fio
 * vertical inset de 20 px entre os KPIs (DESIGN.md §4.4).
 */
const CELULA_KPI =
  "relative min-w-0 border-r border-b border-line-soft bg-surface xl:border-0 " +
  "xl:[&+&]:before:absolute xl:[&+&]:before:inset-y-5 xl:[&+&]:before:left-0 xl:[&+&]:before:w-px xl:[&+&]:before:bg-line-soft";

export function Kpi({ rotulo, rotuloCurto, valor, meta, medidor, para }: KpiProps) {
  const conteudo = (
    <>
      <RotuloKpi rotulo={rotulo} {...(rotuloCurto ? { rotuloCurto } : {})} />
      <NumeroKpi valor={valor} className="mt-1.5 block md:mt-2.5" />
      {meta && (
        <span className="mt-1 block type-meta text-ink-3 md:mt-2 md:whitespace-nowrap">{meta}</span>
      )}
      {medidor && <Medidor {...medidor} />}
    </>
  );
  const classes = "block h-full p-4 md:px-5 md:py-5 md:pl-[22px]";
  return (
    <li className={CELULA_KPI}>
      {para ? (
        <Link
          to={para}
          className={cn(
            classes,
            "transition-colors duration-(--dur-fast) hover:bg-surface-hover focus-visible:-outline-offset-2",
          )}
        >
          {conteudo}
        </Link>
      ) : (
        <div className={classes}>{conteudo}</div>
      )}
    </li>
  );
}

interface KpiCardProps {
  /** Nome da faixa para leitor de tela ("Indicadores da carteira"). */
  rotulo: string;
  children: ReactNode;
  className?: string;
}

/** Faixa de até 6 KPIs num card: 2 colunas no mobile, 3 no md, 6 no xl (DESIGN.md §4.4/§5.3). */
export function KpiCard({ rotulo, children, className }: KpiCardProps) {
  return (
    <section
      aria-label={rotulo}
      className={cn("overflow-hidden rounded-xl border border-line bg-surface", className)}
    >
      <ul className="-mr-px -mb-px grid grid-cols-2 md:grid-cols-3 xl:m-0 xl:grid-cols-6">
        {children}
      </ul>
    </section>
  );
}

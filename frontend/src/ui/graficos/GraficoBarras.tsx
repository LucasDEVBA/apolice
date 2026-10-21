import { ChartColumn, Table2 } from "lucide-react";
import { useId, useState } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Rectangle,
  ReferenceLine,
  Tooltip,
  XAxis,
  YAxis,
  type BarShapeProps,
  type TooltipContentProps,
} from "recharts";

import { useMediaQuery } from "../tema/useMediaQuery";
import { useReducedMotion } from "../tema/useReducedMotion";
import { cn } from "../utils/cn";
import {
  anoDoMes,
  formatarCompacto,
  formatarData,
  formatarMesAno,
  formatarMesEixo,
  formatarMesPorExtenso,
  formatarMoeda,
} from "../utils/formatar";

export interface PontoMensal {
  /** "2026-10" */
  mes: string;
  /** Valor realizado (R$). */
  valor: number;
  /** Total projetado do mês corrente (≥ valor): o restante aparece tracejado. */
  projecao?: number;
  /** Linha extra do tooltip ("96 apólices"). */
  detalhe?: string;
}

interface Linha extends PontoMensal {
  restante: number;
  corrente: boolean;
}

export interface GraficoBarrasProps {
  dados: readonly PontoMensal[];
  /** Descrição do gráfico para leitor de tela e caption da tabela. */
  rotulo: string;
  /** Mês corrente (parcial) em --chart-accent. */
  mesCorrente?: string;
  /** DateOnly do último dia apurado do mês corrente ("parcial até 21/10"). */
  parcialAte?: string;
  /** Linha de média tracejada com rótulo ("Média 340"). */
  media?: number;
  rotuloMedia?: string;
  /** Nome da série de média na legenda. */
  legendaMedia?: string;
  altura?: number;
  className?: string;
}

const ALTURA_PADRAO = 260;
const MARCAS_EIXO_Y = 4;
const DURACAO_ENTRADA_MS = 400;
// Mês sem dado vira um traço de 2 px na base, sem buraco no gráfico (DESIGN.md §4.15).
const ALTURA_MINIMA_BARRA = 2;
// Espaço à direita do plot para o rótulo da média ("Média 4,1 mil" a 12 px), fora das barras.
const LARGURA_ROTULO_MEDIA = 88;
const EIXO_Y = { largo: 44, estreito: 34 } as const;
const BARRA = { largo: 26, estreito: 14 } as const;

function valorDeEixo(valor: number): string {
  return formatarCompacto(valor).texto;
}

interface TickProps {
  x?: number | string;
  y?: number | string;
  payload?: { value: string; index: number };
  mesCorrente?: string | undefined;
}

/**
 * Valor do eixo Y numa linha só ("8 mil"): o tick padrão do Recharts quebra o texto na largura do
 * eixo, e o "mil" caía para a linha de baixo no mobile.
 */
function TickValor({
  x = 0,
  y = 0,
  payload,
}: {
  x?: number | string;
  y?: number | string;
  payload?: { value: number };
}) {
  if (!payload) return null;
  return (
    <text x={x} y={y} dy="0.355em" textAnchor="end" className="fill-ink-3 text-xs">
      {valorDeEixo(payload.value)}
    </text>
  );
}

/** Mês abreviado; o ano aparece sob o primeiro mês e sob janeiro. */
function TickMes({ x = 0, y = 0, payload, mesCorrente }: TickProps) {
  if (!payload) return null;
  const mes = payload.value;
  const mostrarAno = payload.index === 0 || mes.endsWith("-01");
  const corrente = mes === mesCorrente;
  return (
    <g transform={`translate(${String(x)},${String(y)})`}>
      <text
        dy={14}
        textAnchor="middle"
        className={cn("text-xs", corrente ? "fill-ink font-semibold" : "fill-ink-3")}
      >
        {formatarMesEixo(mes)}
      </text>
      {mostrarAno && (
        <text dy={30} textAnchor="middle" className="fill-ink-3 text-xs">
          {anoDoMes(mes)}
        </text>
      )}
    </g>
  );
}

function ConteudoTooltip({
  active,
  payload,
  parcialAte,
}: Partial<TooltipContentProps<number, string>> & { parcialAte?: string | undefined }) {
  const linha = payload?.[0]?.payload as Linha | undefined;
  if (!active || !linha) return null;
  const cabecalho = [formatarMesAno(linha.mes), linha.detalhe].filter(Boolean).join(" · ");
  return (
    <div className="rounded-md border border-brand-line bg-brand px-3.5 py-2.5 text-on-navy shadow-overlay">
      <p className="text-xs text-on-navy-2 tnum">{cabecalho}</p>
      <p className="mt-0.5 fig text-[1.0625rem] text-on-navy">{formatarMoeda(linha.valor)}</p>
      {linha.projecao !== undefined && (
        <p className="mt-0.5 text-xs text-on-navy-2 tnum">
          Projeção {formatarMoeda(linha.projecao)}
        </p>
      )}
      {linha.corrente && parcialAte && (
        <p className="mt-0.5 text-xs text-gold-on-navy tnum">
          parcial até {formatarData(parcialAte).slice(0, 5)}
        </p>
      )}
    </div>
  );
}

function Legenda({
  temCorrente,
  temProjecao,
  media,
}: {
  temCorrente: boolean;
  temProjecao: boolean;
  media?: string | undefined;
}) {
  return (
    <ul className="flex flex-wrap items-center gap-x-4 gap-y-1.5 type-meta text-ink-2" aria-hidden>
      <li className="inline-flex items-center gap-1.5">
        <span className="size-2.5 rounded-[3px] bg-chart-1" />
        Emitido
      </li>
      {temCorrente && (
        <li className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-[3px] bg-chart-accent" />
          Mês corrente
        </li>
      )}
      {temProjecao && (
        <li className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-[3px] border border-dashed border-chart-accent bg-chart-projection" />
          Projeção do mês
        </li>
      )}
      {media && (
        <li className="inline-flex items-center gap-1.5">
          <span className="w-4 border-t border-dashed border-chart-ref" />
          {media}
        </li>
      )}
    </ul>
  );
}

/**
 * Alterna gráfico e tabela. Rótulo variável ("Ver como tabela"/"Ver como gráfico") SEM
 * aria-pressed: as duas coisas juntas fariam o leitor ler "Ver como gráfico, pressionado".
 */
function BotaoAlternar({
  comoTabela,
  idAlvo,
  aoAlternar,
}: {
  comoTabela: boolean;
  idAlvo: string;
  aoAlternar: () => void;
}) {
  return (
    <button
      type="button"
      aria-controls={idAlvo}
      onClick={aoAlternar}
      className="inline-flex h-11 items-center gap-1.5 rounded-md px-2 text-label font-semibold text-link hover:bg-hover-tint md:h-10"
    >
      {comoTabela ? <ChartColumn size={15} aria-hidden /> : <Table2 size={15} aria-hidden />}
      {comoTabela ? "Ver como gráfico" : "Ver como tabela"}
    </button>
  );
}

function TabelaDoGrafico({
  linhas,
  rotulo,
  temProjecao,
}: {
  linhas: readonly Linha[];
  rotulo: string;
  temProjecao: boolean;
}) {
  return (
    <table className="w-full border-collapse text-sm tnum">
      <caption className="sr-only">{rotulo}</caption>
      <thead>
        <tr className="border-y border-line bg-surface-2 text-meta text-ink-2">
          <th scope="col" className="px-3 py-2 text-left font-semibold">
            Mês
          </th>
          <th scope="col" className="px-3 py-2 text-right font-semibold">
            Valor
          </th>
          {temProjecao && (
            <th scope="col" className="px-3 py-2 text-right font-semibold">
              Projeção
            </th>
          )}
        </tr>
      </thead>
      <tbody>
        {linhas.map((l) => (
          <tr key={l.mes} className="border-b border-line-soft">
            <th scope="row" className="px-3 py-2 text-left font-normal text-ink">
              {formatarMesPorExtenso(l.mes)}
              {l.corrente && <span className="text-ink-3"> (parcial)</span>}
            </th>
            <td className="px-3 py-2 text-right text-ink">{formatarMoeda(l.valor)}</td>
            {temProjecao && (
              <td className="px-3 py-2 text-right text-ink-2">
                {l.projecao === undefined ? "—" : formatarMoeda(l.projecao)}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

interface BarrasProps {
  linhas: Linha[];
  rotulo: string;
  altura: number;
  mesCorrente: string | undefined;
  parcialAte: string | undefined;
  media: number | undefined;
  textoMedia: string | undefined;
  temProjecao: boolean;
}

function BarrasRecharts({
  linhas,
  rotulo,
  altura,
  mesCorrente,
  parcialAte,
  media,
  textoMedia,
  temProjecao,
}: BarrasProps) {
  const reduzido = useReducedMotion();
  const largo = useMediaQuery("(width >= 48rem)");
  const tamanho = largo ? "largo" : "estreito";
  // No mobile o rótulo da média sai do plot (o valor fica na legenda) para não cobrir barras.
  const rotuloNoPlot = largo && media !== undefined;
  const animacao = { isAnimationActive: !reduzido, animationDuration: DURACAO_ENTRADA_MS };
  return (
    <div
      className={cn(
        // No mobile o gráfico sangra 8 px no padding do card: sobra respiro entre os 12 meses.
        "-mx-2 md:mx-0",
        "[&_.recharts-cartesian-axis-line]:stroke-chart-axis [&_.recharts-cartesian-grid_line]:stroke-chart-grid",
        "[&_.recharts-reference-line_line]:stroke-chart-ref [&_.recharts-tooltip-cursor]:fill-hover-tint",
        "[&_.recharts-surface]:overflow-visible",
      )}
    >
      <BarChart
        responsive
        width="100%"
        height={altura}
        data={linhas}
        margin={{ top: 18, right: rotuloNoPlot ? LARGURA_ROTULO_MEDIA : 4, bottom: 4, left: 0 }}
        accessibilityLayer
        title={rotulo}
        barCategoryGap="22%"
      >
        <CartesianGrid vertical={false} strokeWidth={1} />
        <XAxis
          dataKey="mes"
          tickLine={false}
          interval={0}
          height={40}
          tick={<TickMes mesCorrente={mesCorrente} />}
        />
        <YAxis
          width={EIXO_Y[tamanho]}
          tickCount={MARCAS_EIXO_Y + 1}
          tickLine={false}
          axisLine={false}
          tickMargin={largo ? 8 : 4}
          tick={<TickValor />}
        />
        <Tooltip
          content={<ConteudoTooltip parcialAte={parcialAte} />}
          cursor={{ className: "recharts-tooltip-cursor" }}
        />
        {media !== undefined && (
          <ReferenceLine
            y={media}
            strokeDasharray="3 4"
            {...(rotuloNoPlot
              ? {
                  label: {
                    value: textoMedia ?? "",
                    position: "right",
                    offset: 8,
                    className: "fill-chart-ref text-xs font-semibold",
                  },
                }
              : {})}
          />
        )}
        <Bar
          dataKey="valor"
          stackId="mes"
          barSize={BARRA[tamanho]}
          minPointSize={ALTURA_MINIMA_BARRA}
          radius={[4, 4, 0, 0]}
          {...animacao}
          shape={(props: BarShapeProps) => (
            <Rectangle
              {...props}
              className={linhas[props.index]?.corrente ? "fill-chart-accent" : "fill-chart-1"}
            />
          )}
        />
        {temProjecao && (
          <Bar
            dataKey="restante"
            stackId="mes"
            barSize={BARRA[tamanho]}
            radius={[4, 4, 0, 0]}
            {...animacao}
            shape={(props: BarShapeProps) => (
              <Rectangle
                {...props}
                className="fill-chart-projection stroke-chart-accent"
                strokeDasharray="3 3"
              />
            )}
          />
        )}
      </BarChart>
    </div>
  );
}

/**
 * Barras mensais (DESIGN.md §4.15) com alternativa acessível "Ver como tabela". Cores vêm de classes
 * de token (fill-chart-1…), então o gráfico troca de tema sem re-renderizar.
 */
export function GraficoBarras({
  dados,
  rotulo,
  mesCorrente,
  parcialAte,
  media,
  rotuloMedia,
  legendaMedia = "Média mensal",
  altura = ALTURA_PADRAO,
  className,
}: GraficoBarrasProps) {
  const [comoTabela, setComoTabela] = useState(false);
  const largo = useMediaQuery("(width >= 48rem)");
  const idConteudo = useId();

  const linhas: Linha[] = dados.map((p) => ({
    ...p,
    restante: Math.max(0, (p.projecao ?? p.valor) - p.valor),
    corrente: p.mes === mesCorrente,
  }));
  const temProjecao = linhas.some((l) => l.restante > 0);
  const textoMedia =
    media === undefined ? undefined : (rotuloMedia ?? `Média ${valorDeEixo(media)}`);
  // Desktop: o valor está no rótulo da linha; mobile: vai junto do nome na legenda.
  const legendaDaMedia =
    media === undefined
      ? undefined
      : largo
        ? legendaMedia
        : `${legendaMedia} · ${valorDeEixo(media)}`;

  return (
    <div className={cn("flex flex-col gap-3", className)}>
      <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
        <Legenda
          temCorrente={mesCorrente !== undefined}
          temProjecao={temProjecao}
          media={legendaDaMedia}
        />
        <BotaoAlternar
          comoTabela={comoTabela}
          idAlvo={idConteudo}
          aoAlternar={() => setComoTabela((v) => !v)}
        />
      </div>
      <div id={idConteudo}>
        {comoTabela ? (
          <TabelaDoGrafico linhas={linhas} rotulo={rotulo} temProjecao={temProjecao} />
        ) : (
          <BarrasRecharts
            linhas={linhas}
            rotulo={rotulo}
            altura={altura}
            mesCorrente={mesCorrente}
            parcialAte={parcialAte}
            media={media}
            textoMedia={textoMedia}
            temProjecao={temProjecao}
          />
        )}
      </div>
    </div>
  );
}

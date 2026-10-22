import { ArrowRight, Minus, MoveDownRight, MoveUpRight, RotateCw } from "lucide-react";
import { useEffect, useId, useRef, useState, type ReactNode } from "react";

import { Gravura } from "../marca/Marca";
import { anunciar } from "../utils/anunciar";
import { cn } from "../utils/cn";
import {
  formatarData,
  formatarFator,
  formatarHora,
  formatarMoeda,
  formatarPercentual,
} from "../utils/formatar";
import { calcularParcelas } from "../utils/parcelas";
import { SegmentedControl } from "./SegmentedControl";
import { Select } from "./Select";
import { Money } from "./Valores";

export interface ItemCalculo {
  codigo: string;
  nome: string;
  premio: number;
  /** Prêmio fixo (Carro reserva, Assistência): não sofre fator (SPEC §4.2). */
  fixo?: boolean;
  /** Realça a linha que acabou de mudar (900 ms). */
  alterado?: boolean;
}

export interface FatorCalculo {
  id: string;
  /** Descrição de negócio: "Condutor de 18 a 25 anos". */
  descricao: string;
  detalhe?: string;
  fator: number;
  /** "Casco, RCF-DM e RCF-DC". */
  aplicaSobre?: string;
  /** Efeito estimado em R$, só quando calculável no cliente. */
  efeito?: number;
}

export type EstadoCalculo = "atualizado" | "recalculando" | "desatualizado";

export interface PainelCalculoProps {
  /** "Tarifa Auto". */
  tarifa: string;
  /** DateOnly de referência da tarifa. */
  referencia: string;
  /** Instante da última simulação ("Ao vivo · 14:36:08"). */
  atualizadoEm?: Date | string | undefined;
  itens: readonly ItemCalculo[];
  fatores: readonly FatorCalculo[];
  premioLiquido: number;
  /** Fração vinda do DTO (0,0738 Auto/Resid; 0,0038 Vida). */
  aliquotaIof: number;
  iof: number;
  premioTotal: number;
  maxParcelas: number;
  parcelas: number;
  aoMudarParcelas: (parcelas: number) => void;
  /** DateOnly: cotação válida até. */
  validaAte: string;
  estado?: EstadoCalculo;
  aoTentarDeNovo?: () => void;
  /** CTA principal ("Revisar cotação"). */
  acao?: ReactNode;
  className?: string;
}

const PARCELAS_SUGERIDAS = [1, 3, 6, 10] as const;
const OUTRO = "outro";
const ATRASO_ANUNCIO_MS = 700;

/*
 * Durante o recálculo só os VALORES em --ink/--on-navy esmaecem: a 60% eles ainda passam 4,5:1
 * (4,6 e 6,3). Rótulos, notas e chips em tons secundários cairiam abaixo disso (DESIGN.md §4.12).
 */
const ESMAECE = "transition-opacity duration-(--dur-base)";

// ---------------------------------------------------------------- cabeçalho

interface CabecalhoProps {
  idTitulo: string;
  tarifa: string;
  referencia: string;
  atualizadoEm: Date | string | undefined;
  estado: EstadoCalculo;
  aoTentarDeNovo: (() => void) | undefined;
}

function IndicadorAoVivo({ atualizadoEm }: { atualizadoEm: Date | string }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-xs font-semibold whitespace-nowrap text-ok tnum">
      <span
        aria-hidden
        className="size-[7px] rounded-full bg-ok shadow-[0_0_0_3px_color-mix(in_srgb,var(--ok)_15%,transparent)]"
      />
      Ao vivo · {formatarHora(atualizadoEm, true)}
    </span>
  );
}

/** Título e indicador numa linha (quebram juntos se faltar espaço); a referência fica embaixo. */
function CabecalhoCalculo({
  idTitulo,
  tarifa,
  referencia,
  atualizadoEm,
  estado,
  aoTentarDeNovo,
}: CabecalhoProps) {
  return (
    <header className="px-6 pt-[22px] pb-1">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h2 id={idTitulo} className="type-title-card whitespace-nowrap text-ink">
          Cálculo do prêmio
        </h2>
        {estado === "desatualizado" ? (
          <button
            type="button"
            onClick={aoTentarDeNovo}
            // Alvo ampliado por pseudo-elemento: 44 px de altura sem mexer no layout.
            className="relative inline-flex items-center gap-1.5 rounded-xs text-xs font-semibold whitespace-nowrap text-warn before:absolute before:-inset-x-2 before:-inset-y-3.5 hover:underline"
          >
            <RotateCw size={13} aria-hidden />
            Desatualizado · Tentar de novo
          </button>
        ) : (
          atualizadoEm && <IndicadorAoVivo atualizadoEm={atualizadoEm} />
        )}
      </div>
      <p className="mt-0.5 text-label text-ink-3 tnum">
        {tarifa} · referência <span className="whitespace-nowrap">{formatarData(referencia)}</span>
      </p>
    </header>
  );
}

// ---------------------------------------------------------------- linhas

interface LinhaGuiaProps {
  rotulo: ReactNode;
  valor: ReactNode;
  forte?: boolean;
  nota?: string;
  recalculando: boolean;
  className?: string;
}

/**
 * rótulo ······ valor. A guia pontilhada é o ::after do <dt> (DESIGN.md §3.12): o grupo do <dl>
 * só pode ter <dt> e <dd>, então nada de elemento extra entre os dois.
 */
function LinhaGuia({
  rotulo,
  valor,
  forte = false,
  nota,
  recalculando,
  className,
}: LinhaGuiaProps) {
  return (
    <div
      className={cn(
        "flex items-baseline py-1 text-item text-ink tnum",
        forte && "font-semibold",
        className,
      )}
    >
      <dt className="flex min-w-0 flex-1 items-baseline after:mx-2 after:min-w-4 after:flex-1 after:-translate-y-1 after:border-b after:border-dotted after:border-guide after:content-['']">
        {rotulo}
      </dt>
      <dd className="shrink-0 whitespace-nowrap">
        {nota && <span className="mr-1.5 text-xs font-normal text-ink-3">{nota}</span>}
        <span className={cn(ESMAECE, recalculando && "opacity-60")}>{valor}</span>
      </dd>
    </div>
  );
}

function TituloBloco({ titulo, nota }: { titulo: string; nota: string }) {
  return (
    <h3 className="mb-1.5 flex justify-between gap-3 text-meta font-semibold text-ink-2">
      {titulo} <span className="text-right font-medium text-ink-3">{nota}</span>
    </h3>
  );
}

function ListaItens({
  itens,
  recalculando,
}: {
  itens: readonly ItemCalculo[];
  recalculando: boolean;
}) {
  return (
    <div className="px-6 pt-3.5 pb-1">
      <TituloBloco titulo="Coberturas" nota="prêmio anual" />
      <dl>
        {itens.map((item) => (
          <LinhaGuia
            key={item.codigo}
            rotulo={item.nome}
            valor={formatarMoeda(item.premio)}
            recalculando={recalculando}
            className={cn("-mx-2 rounded-[6px] px-2", item.alterado && "anima-realce")}
            {...(item.fixo ? { nota: "fixo" } : {})}
          />
        ))}
      </dl>
    </div>
  );
}

function ChipFator({ fator }: { fator: number }) {
  const agrava = fator > 1;
  const desconta = fator < 1;
  const Seta = agrava ? MoveUpRight : desconta ? MoveDownRight : Minus;
  const sentido = agrava ? "agrava" : desconta ? "desconta" : "neutro";
  return (
    <span
      className={cn(
        "inline-flex h-6 shrink-0 items-center gap-1 rounded-full px-2 text-label font-semibold tnum",
        agrava && "bg-warn-bg text-warn",
        desconta && "bg-ok-bg text-ok",
        !agrava && !desconta && "bg-neutral-bg text-neutral",
      )}
    >
      <Seta size={13} strokeWidth={2} aria-hidden />
      <span className="sr-only">{sentido} </span>
      {formatarFator(fator)}
    </span>
  );
}

function LinhaFator({ fator }: { fator: FatorCalculo }) {
  return (
    <li className="grid grid-cols-[1fr_auto] gap-3 border-b border-line-soft py-2 last:border-b-0">
      <div className="min-w-0">
        <p className="text-item font-semibold text-ink">{fator.descricao}</p>
        {fator.detalhe && <p className="text-xs text-ink-3">{fator.detalhe}</p>}
        {fator.aplicaSobre && <p className="text-xs text-ink-3">sobre {fator.aplicaSobre}</p>}
      </div>
      <div className="flex flex-col items-end gap-1">
        <ChipFator fator={fator.fator} />
        {fator.efeito !== undefined && (
          <span className="text-xs text-ink-3 tnum">
            <span className="sr-only">efeito estimado </span>
            <Money valor={fator.efeito} comSinal className="font-semibold text-ink-2" />
          </span>
        )}
      </div>
    </li>
  );
}

function ListaFatores({ fatores }: { fatores: readonly FatorCalculo[] }) {
  if (fatores.length === 0) return null;
  return (
    <div className="px-6 pt-3.5 pb-1">
      <TituloBloco titulo="Fatores aplicados" nota="sobre os itens com taxa" />
      <ul>
        {fatores.map((fator) => (
          <LinhaFator key={fator.id} fator={fator} />
        ))}
      </ul>
      {fatores.some((f) => f.efeito !== undefined) && (
        <p className="mt-1 text-right text-xs text-ink-3">efeito estimado, na ordem exibida</p>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- total

interface BlocoTotalProps {
  premioTotal: number;
  maxParcelas: number;
  validaAte: string;
  recalculando: boolean;
}

/**
 * Bloco do total em --brand-surface, de ponta a ponta no card. No escuro o fio dourado em cima e
 * embaixo (--brand-surface-line) o separa do card, que também é marinho (DESIGN.md §3.3).
 */
function BlocoTotal({ premioTotal, maxParcelas, validaAte, recalculando }: BlocoTotalProps) {
  const maximo = calcularParcelas(premioTotal, maxParcelas);
  return (
    <div className="on-navy relative overflow-hidden border-y border-brand-line bg-brand px-6 py-5 text-on-navy">
      <Gravura tamanho="sm" className="absolute -top-[150px] -right-[120px] size-[260px]" />
      <p className="relative text-label font-medium text-on-navy-2">Prêmio total</p>
      <Money
        valor={premioTotal}
        variante="heroi"
        sobreMarinho
        className={cn(
          "relative mt-1 block type-fig-hero text-on-navy",
          ESMAECE,
          recalculando && "opacity-60",
        )}
      />
      <p className="relative mt-1.5 text-sm font-medium text-gold-on-navy tnum">
        {maxParcelas > 1
          ? `ou até ${String(maxParcelas)}× de ${formatarMoeda(maximo.demais)} sem juros`
          : "pagamento à vista"}
      </p>
      <p className="relative mt-3.5 border-t border-on-navy/10 pt-3 type-meta text-on-navy-3 tnum">
        Cotação válida até {formatarData(validaAte)}
      </p>
    </div>
  );
}

// ---------------------------------------------------------------- parcelamento

function sugeridas(maximo: number): number[] {
  return [...new Set([...PARCELAS_SUGERIDAS, maximo])]
    .filter((n) => n >= 1 && n <= maximo)
    .sort((a, b) => a - b);
}

interface SeletorParcelasProps {
  maxParcelas: number;
  parcelas: number;
  premioTotal: number;
  aoMudarParcelas: (parcelas: number) => void;
}

/**
 * Segmentado 1× · 3× · 6× · 10× (filtrado por MaxParcelas) + "Outro", que abre a lista completa
 * (DESIGN.md §4.10). A ajuda repete a regra do SPEC: a 1ª parcela absorve os centavos.
 */
function SeletorParcelas({
  maxParcelas,
  parcelas,
  premioTotal,
  aoMudarParcelas,
}: SeletorParcelasProps) {
  const id = useId();
  const atalhos = sugeridas(maxParcelas);
  const temOutras = maxParcelas > atalhos.length;
  const [escolhendoOutro, setEscolhendoOutro] = useState(false);
  const modoOutro = temOutras && (escolhendoOutro || !atalhos.includes(parcelas));
  const plano = calcularParcelas(premioTotal, parcelas);
  const opcoes = [
    ...atalhos.map((n) => ({ valor: String(n), rotulo: `${String(n)}×` })),
    ...(temOutras ? [{ valor: OUTRO, rotulo: "Outro" }] : []),
  ];
  const todas = Array.from({ length: maxParcelas }, (_, i) => ({
    valor: String(i + 1),
    rotulo: `${String(i + 1)}×`,
  }));

  return (
    <div className="flex flex-col gap-1.5">
      <span id={`${id}-rotulo`} className="type-label text-ink">
        Simular parcelamento
      </span>
      <SegmentedControl
        aria-labelledby={`${id}-rotulo`}
        aria-describedby={`${id}-ajuda`}
        opcoes={opcoes}
        valor={modoOutro ? OUTRO : String(parcelas)}
        aoMudar={(valor) => {
          setEscolhendoOutro(valor === OUTRO);
          if (valor !== OUTRO) aoMudarParcelas(Number(valor));
        }}
        larguraTotal
      />
      {modoOutro && (
        <Select
          aria-label="Número de parcelas"
          opcoes={todas}
          valor={String(parcelas)}
          aoMudar={(valor) => aoMudarParcelas(Number(valor))}
          tamanho="sm"
        />
      )}
      <p id={`${id}-ajuda`} className="type-caption font-normal text-ink-3 tnum">
        {parcelas === 1
          ? `À vista: ${formatarMoeda(plano.primeira)}.`
          : `1ª de ${formatarMoeda(plano.primeira)} + ${String(parcelas - 1)}× de ${formatarMoeda(plano.demais)}. A 1ª parcela absorve o ajuste de centavos.`}{" "}
        A escolha final é feita na emissão.
      </p>
    </div>
  );
}

// ---------------------------------------------------------------- painel

/** Anuncia só o total, com atraso, para não repetir a cada tecla digitada. */
function useAnuncioDoTotal(premioTotal: number) {
  const primeiroRender = useRef(true);
  useEffect(() => {
    if (primeiroRender.current) {
      primeiroRender.current = false;
      return;
    }
    const timer = window.setTimeout(() => {
      anunciar(`Prêmio total atualizado: ${formatarMoeda(premioTotal)}`);
    }, ATRASO_ANUNCIO_MS);
    return () => window.clearTimeout(timer);
  }, [premioTotal]);
}

/**
 * Memória de cálculo ao vivo (DESIGN.md §4.12), fiel ao SPEC §4.2: fatores multiplicam só os itens
 * com taxa; prêmio líquido = soma dos itens; IOF com a alíquota do DTO; 1ª parcela absorve centavos.
 */
export function PainelCalculo({
  tarifa,
  referencia,
  atualizadoEm,
  itens,
  fatores,
  premioLiquido,
  aliquotaIof,
  iof,
  premioTotal,
  maxParcelas,
  parcelas,
  aoMudarParcelas,
  validaAte,
  estado = "atualizado",
  aoTentarDeNovo,
  acao,
  className,
}: PainelCalculoProps) {
  const id = useId();
  const recalculando = estado === "recalculando";
  useAnuncioDoTotal(premioTotal);

  return (
    <section
      aria-labelledby={`${id}-titulo`}
      aria-busy={recalculando || undefined}
      className={cn(
        // Gruda abaixo da topbar fixa de 64 px, com 24 px de respiro.
        "overflow-hidden rounded-xl border border-line bg-surface lg:sticky lg:top-[calc(var(--topbar-h)+24px)]",
        className,
      )}
    >
      <CabecalhoCalculo
        idTitulo={`${id}-titulo`}
        tarifa={tarifa}
        referencia={referencia}
        atualizadoEm={atualizadoEm}
        estado={estado}
        aoTentarDeNovo={aoTentarDeNovo}
      />
      <ListaItens itens={itens} recalculando={recalculando} />
      <ListaFatores fatores={fatores} />
      <dl className="mx-6 mt-2.5 border-t border-line pt-3 pb-3.5">
        <LinhaGuia
          rotulo="Prêmio líquido"
          valor={formatarMoeda(premioLiquido)}
          forte
          recalculando={recalculando}
        />
        <LinhaGuia
          rotulo={`IOF ${formatarPercentual(aliquotaIof)}`}
          valor={formatarMoeda(iof)}
          recalculando={recalculando}
        />
      </dl>
      <BlocoTotal
        premioTotal={premioTotal}
        maxParcelas={maxParcelas}
        validaAte={validaAte}
        recalculando={recalculando}
      />
      <div className="flex flex-col gap-3 px-6 pt-5 pb-6">
        <SeletorParcelas
          maxParcelas={maxParcelas}
          parcelas={parcelas}
          premioTotal={premioTotal}
          aoMudarParcelas={aoMudarParcelas}
        />
        {acao}
      </div>
    </section>
  );
}

interface BarraTotalMobileProps {
  premioTotal: number;
  aoVerCalculo: () => void;
  /** false na vitrine (barra estática em vez de fixa no rodapé). */
  fixa?: boolean;
}

/** Abaixo de lg o painel vira barra inferior de 64 px que abre a memória num bottom sheet. */
export function BarraTotalMobile({
  premioTotal,
  aoVerCalculo,
  fixa = true,
}: BarraTotalMobileProps) {
  return (
    <div
      className={cn(
        "on-navy flex h-16 items-center justify-between gap-3 bg-brand px-4 text-on-navy",
        // Fio dourado no escuro (--brand-surface-line), como toda superfície de marca.
        fixa &&
          "fixed inset-x-0 bottom-0 z-30 h-[calc(64px+env(safe-area-inset-bottom))] border-t border-brand-line pb-[env(safe-area-inset-bottom)] lg:hidden",
        !fixa && "rounded-xl border border-brand-line",
      )}
    >
      <p className="flex flex-col">
        <span className="text-xs text-on-navy-2">Prêmio total</span>
        <span className="text-campo font-semibold tnum">{formatarMoeda(premioTotal)}</span>
      </p>
      <button
        type="button"
        onClick={aoVerCalculo}
        className="inline-flex h-11 items-center gap-1.5 rounded-md px-3 text-sm font-semibold text-gold-on-navy hover:bg-hover-tint-navy"
      >
        Ver cálculo
        <ArrowRight size={15} aria-hidden />
      </button>
    </div>
  );
}

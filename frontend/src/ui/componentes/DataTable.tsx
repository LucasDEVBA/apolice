import { ArrowDown, ArrowUp, ArrowUpDown } from "lucide-react";
import type { MouseEvent, ReactNode } from "react";
import { Link, useNavigate } from "react-router";

import { cn } from "../utils/cn";
import { Skeleton } from "./Skeleton";

export type DirecaoOrdenacao = "asc" | "desc";

export interface Ordenacao {
  coluna: string;
  direcao: DirecaoOrdenacao;
}

export interface ColunaTabela<T> {
  id: string;
  cabecalho: string;
  celula: (linha: T) => ReactNode;
  /** Alinha à direita com algarismos tabulares (valores, datas, contagens). */
  numerica?: boolean;
  ordenavel?: boolean;
  /** Código/ID em mono-id com peso de célula de código. */
  codigo?: boolean;
  classe?: string;
}

export interface DataTableProps<T> {
  /** Caption da tabela (pode ficar visualmente oculta). */
  legenda: string;
  legendaVisivel?: boolean;
  colunas: readonly ColunaTabela<T>[];
  linhas: readonly T[];
  chave: (linha: T) => string;
  /** Destino da linha. A célula da 1ª coluna deve conter o <Link> real (alvo acessível). */
  hrefDaLinha?: (linha: T) => string;
  /**
   * Conteúdo do card que substitui a linha abaixo de 768 px. Com hrefDaLinha, o card inteiro vira
   * link: não coloque links ou botões aqui dentro.
   */
  cartao?: (linha: T) => ReactNode;
  ordenacao?: Ordenacao | undefined;
  aoOrdenar?: (coluna: string) => void;
  selecionada?: (linha: T) => boolean;
  carregando?: boolean;
  vazio?: ReactNode;
  /** Linhas <tr> de totais (tfoot). */
  rodape?: ReactNode;
  /** Altura máxima com rolagem interna e cabeçalho fixo (ex.: "28rem"). */
  alturaMaxima?: string;
  /** Busca, filtros em chips, densidade e exportar (DESIGN.md §4.5); fica visível no vazio. */
  barraDeFerramentas?: ReactNode;
  className?: string;
}

const LINHAS_FANTASMA = 5;
const CARTOES_FANTASMA = 3;
const SELETOR_INTERATIVO =
  "a, button, input, select, textarea, label, [role='button'], [role='checkbox']";
const CELULA_BORDA = "first:pl-(--card-pad) last:pr-(--card-pad)";

function ariaSort(coluna: string, ordenacao?: Ordenacao) {
  if (ordenacao?.coluna !== coluna) return undefined;
  return ordenacao.direcao === "asc" ? "ascending" : "descending";
}

function IconeOrdenacao({
  ativa,
  direcao,
}: {
  ativa: boolean;
  direcao?: DirecaoOrdenacao | undefined;
}) {
  if (!ativa) return <ArrowUpDown size={13} className="text-ink-3" aria-hidden />;
  return direcao === "asc" ? (
    <ArrowUp size={13} aria-hidden />
  ) : (
    <ArrowDown size={13} aria-hidden />
  );
}

interface CabecalhoTabelaProps<T> {
  colunas: readonly ColunaTabela<T>[];
  ordenacao: Ordenacao | undefined;
  aoOrdenar: ((coluna: string) => void) | undefined;
}

/** Cabeçalho --surface-2, fixo na rolagem; ordenação por <button> com aria-sort no <th>. */
function CabecalhoTabela<T>({ colunas, ordenacao, aoOrdenar }: CabecalhoTabelaProps<T>) {
  return (
    <thead>
      <tr>
        {colunas.map((coluna) => (
          <th
            key={coluna.id}
            scope="col"
            aria-sort={coluna.ordenavel ? (ariaSort(coluna.id, ordenacao) ?? "none") : undefined}
            className={cn(
              "sticky top-0 z-[1] border-y border-line bg-surface-2 px-4 py-2.5 text-meta font-semibold whitespace-nowrap text-ink-2",
              CELULA_BORDA,
              coluna.numerica ? "text-right" : "text-left",
            )}
          >
            {coluna.ordenavel && aoOrdenar ? (
              <button
                type="button"
                onClick={() => aoOrdenar(coluna.id)}
                className={cn(
                  "-mx-1 inline-flex items-center gap-1 rounded-xs px-1 hover:text-ink",
                  coluna.numerica && "flex-row-reverse",
                )}
              >
                {coluna.cabecalho}
                <IconeOrdenacao
                  ativa={ordenacao?.coluna === coluna.id}
                  direcao={ordenacao?.direcao}
                />
              </button>
            ) : (
              coluna.cabecalho
            )}
          </th>
        ))}
      </tr>
    </thead>
  );
}

/** 5 linhas fantasma com as mesmas dimensões das reais (CLS zero, DESIGN.md §4.19). */
function CorpoFantasma<T>({ colunas }: { colunas: readonly ColunaTabela<T>[] }) {
  return (
    <tbody>
      {Array.from({ length: LINHAS_FANTASMA }, (_, i) => (
        <tr key={i}>
          {colunas.map((coluna) => (
            <td
              key={coluna.id}
              className={cn("border-b border-line-soft px-4 py-(--row-y)", CELULA_BORDA)}
            >
              <Skeleton className={cn("h-3", coluna.numerica ? "ml-auto w-16" : "w-3/4")} />
            </td>
          ))}
        </tr>
      ))}
    </tbody>
  );
}

interface CorpoTabelaProps<T> {
  colunas: readonly ColunaTabela<T>[];
  linhas: readonly T[];
  chave: (linha: T) => string;
  selecionada: ((linha: T) => boolean) | undefined;
  hrefDaLinha: ((linha: T) => string) | undefined;
}

/** Linha inteira com hover e clique, mas o alvo acessível é o link real na célula do ID. */
function CorpoTabela<T>({ colunas, linhas, chave, selecionada, hrefDaLinha }: CorpoTabelaProps<T>) {
  const navegar = useNavigate();

  function aoClicarLinha(evento: MouseEvent<HTMLElement>, linha: T) {
    if (!hrefDaLinha) return;
    const alvo = evento.target as HTMLElement;
    // Clique em controle da própria linha ou seleção de texto não navegam.
    if (alvo.closest(SELETOR_INTERATIVO) || window.getSelection()?.toString()) return;
    void navegar(hrefDaLinha(linha));
  }

  return (
    <tbody>
      {linhas.map((linha) => {
        const ativa = selecionada?.(linha) ?? false;
        return (
          <tr
            key={chave(linha)}
            onClick={(e) => aoClicarLinha(e, linha)}
            aria-selected={selecionada ? ativa : undefined}
            className={cn(
              "transition-colors duration-(--dur-fast) hover:bg-surface-hover",
              hrefDaLinha && "cursor-pointer",
              ativa &&
                "bg-surface-selected shadow-[inset_2px_0_0_var(--link)] hover:bg-surface-selected",
            )}
          >
            {colunas.map((coluna) => (
              <td
                key={coluna.id}
                className={cn(
                  "h-(--row-min) border-b border-line-soft px-4 py-(--row-y) align-top text-(length:--fs-table) text-ink",
                  CELULA_BORDA,
                  coluna.numerica && "text-right whitespace-nowrap tnum",
                  coluna.codigo && "mono-id font-semibold text-ink-2",
                  coluna.classe,
                )}
              >
                {coluna.celula(linha)}
              </td>
            ))}
          </tr>
        );
      })}
    </tbody>
  );
}

interface CartaoDaLinhaProps<T> {
  linha: T;
  colunas: readonly ColunaTabela<T>[];
  cartao: ((linha: T) => ReactNode) | undefined;
  href: string | undefined;
}

function CartaoDaLinha<T>({ linha, colunas, cartao, href }: CartaoDaLinhaProps<T>) {
  const conteudo = cartao ? (
    cartao(linha)
  ) : (
    <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
      {colunas.map((coluna) => (
        <div key={coluna.id} className="contents">
          <dt className="type-meta text-ink-3">{coluna.cabecalho}</dt>
          <dd className={cn("text-ink", coluna.numerica && "text-right tnum")}>
            {coluna.celula(linha)}
          </dd>
        </div>
      ))}
    </dl>
  );
  const classes = "block px-4 py-3.5";
  // Só o card customizado vira link inteiro: o padrão reaproveita as células, que já têm o link do ID.
  return href && cartao ? (
    <Link
      to={href}
      className={cn(
        classes,
        "transition-colors hover:bg-surface-hover focus-visible:-outline-offset-2",
      )}
    >
      {conteudo}
    </Link>
  ) : (
    <div className={classes}>{conteudo}</div>
  );
}

interface ListaDeCartoesProps<T> {
  legenda: string;
  colunas: readonly ColunaTabela<T>[];
  linhas: readonly T[];
  chave: (linha: T) => string;
  cartao: ((linha: T) => ReactNode) | undefined;
  hrefDaLinha: ((linha: T) => string) | undefined;
  carregando: boolean;
}

/** Abaixo de 768 px cada linha vira card (DESIGN.md §5.3). */
function ListaDeCartoes<T>({
  legenda,
  colunas,
  linhas,
  chave,
  cartao,
  hrefDaLinha,
  carregando,
}: ListaDeCartoesProps<T>) {
  return (
    <ul aria-label={legenda} className="divide-y divide-line-soft border-t border-line md:hidden">
      {carregando
        ? Array.from({ length: CARTOES_FANTASMA }, (_, i) => (
            <li key={i} className="flex flex-col gap-2.5 px-4 py-4">
              <Skeleton className="h-3 w-2/5" />
              <Skeleton className="h-3 w-3/5" />
              <Skeleton className="h-3 w-4/5" />
            </li>
          ))
        : linhas.map((linha) => (
            <li key={chave(linha)}>
              <CartaoDaLinha
                linha={linha}
                colunas={colunas}
                cartao={cartao}
                href={hrefDaLinha?.(linha)}
              />
            </li>
          ))}
    </ul>
  );
}

/**
 * Tabela de lista (DESIGN.md §4.5): barra de ferramentas opcional, cabeçalho --surface-2, números
 * à direita, linha inteira com hover e clique (alvo acessível no link do ID). No mobile vira cards.
 */
export function DataTable<T>({
  legenda,
  legendaVisivel = false,
  colunas,
  linhas,
  chave,
  hrefDaLinha,
  cartao,
  ordenacao,
  aoOrdenar,
  selecionada,
  carregando = false,
  vazio,
  rodape,
  alturaMaxima,
  barraDeFerramentas,
  className,
}: DataTableProps<T>) {
  const semResultado = !carregando && linhas.length === 0 && vazio;
  return (
    <div className={className} aria-busy={carregando || undefined}>
      {barraDeFerramentas}
      {semResultado ? (
        vazio
      ) : (
        <>
          {carregando && <span className="sr-only">Carregando…</span>}
          <div
            className={cn(
              "relative hidden overflow-x-auto md:block",
              alturaMaxima && "overflow-y-auto",
            )}
            style={alturaMaxima ? { maxHeight: alturaMaxima } : undefined}
          >
            <table className="w-full border-collapse tnum">
              <caption
                className={legendaVisivel ? "px-6 pb-3 text-left type-meta text-ink-3" : "sr-only"}
              >
                {legenda}
              </caption>
              <CabecalhoTabela colunas={colunas} ordenacao={ordenacao} aoOrdenar={aoOrdenar} />
              {carregando ? (
                <CorpoFantasma colunas={colunas} />
              ) : (
                <CorpoTabela
                  colunas={colunas}
                  linhas={linhas}
                  chave={chave}
                  selecionada={selecionada}
                  hrefDaLinha={hrefDaLinha}
                />
              )}
              {rodape && <tfoot>{rodape}</tfoot>}
            </table>
          </div>
          <ListaDeCartoes
            legenda={legenda}
            colunas={colunas}
            linhas={linhas}
            chave={chave}
            cartao={cartao}
            hrefDaLinha={hrefDaLinha}
            carregando={carregando}
          />
        </>
      )}
    </div>
  );
}

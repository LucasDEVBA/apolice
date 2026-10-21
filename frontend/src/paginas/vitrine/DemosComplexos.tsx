import {
  Ban,
  ChevronRight,
  Copy,
  Download,
  FilePen,
  Inbox,
  Lock,
  MoreHorizontal,
  Plus,
  Rows3,
  SearchX,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";

import { Breadcrumbs } from "@/ui/componentes/Breadcrumbs";
import { StatusBadge, type StatusApolice } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card, CardBody, CardFooter, CardHeader } from "@/ui/componentes/Card";
import { GrupoCartoesEscolha } from "@/ui/componentes/CartaoEscolha";
import { notificar } from "@/ui/componentes/notificar";
import { DataTable, type ColunaTabela, type Ordenacao } from "@/ui/componentes/DataTable";
import { ConfirmDialog, Dialog } from "@/ui/componentes/Dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/ui/componentes/DropdownMenu";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { ErrorState } from "@/ui/componentes/ErrorState";
import { BarraFerramentasTabela, CampoBusca, ChipFiltro } from "@/ui/componentes/FiltrosTabela";
import { IconButton } from "@/ui/componentes/IconButton";
import { Atencao, Delta, Kpi, KpiCard, NumeroKpi } from "@/ui/componentes/KpiCard";
import { Pagination } from "@/ui/componentes/Pagination";
import { BarraTotalMobile, PainelCalculo } from "@/ui/componentes/PainelCalculo";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { Skeleton, SkeletonTexto } from "@/ui/componentes/Skeleton";
import { StatusRascunho, Stepper } from "@/ui/componentes/Stepper";
import { Tabs } from "@/ui/componentes/Tabs";
import { Timeline } from "@/ui/componentes/Timeline";
import { Tooltip } from "@/ui/componentes/Tooltip";
import { DateText, Money } from "@/ui/componentes/Valores";
import { BarraDistribuicao } from "@/ui/graficos/BarraDistribuicao";
import { GraficoBarras } from "@/ui/graficos/GraficoBarras";
import { definirDensidade, useDensidade } from "@/ui/tema/preferencias";
import { formatarMoeda } from "@/ui/utils/formatar";

import {
  APOLICES,
  CALCULO_AUTO,
  EVENTOS_APOLICE,
  FATORES_AUTO,
  HOJE,
  ITENS_AUTO,
  MEDIA_MENSAL,
  PREMIO_MENSAL,
  type ApoliceResumo,
} from "./dados";

export function DemoKpis() {
  return (
    <KpiCard rotulo="Indicadores da carteira">
      <Kpi
        rotulo="Apólices ativas"
        valor={{ tipo: "inteiro", valor: 10 }}
        meta={<Delta valor={2}>novas no mês</Delta>}
        para="/apolices"
      />
      <Kpi
        rotulo="Prêmio de outubro"
        valor={{ tipo: "moeda", valor: 28640.5 }}
        meta={
          <Delta valor={0.061} formato="percentual">
            vs. 1–21/set
          </Delta>
        }
        para="/apolices"
      />
      <Kpi
        rotulo="Cotações em aberto"
        valor={{ tipo: "inteiro", valor: 7 }}
        meta={<Atencao numero={2}>expiram em 48 h</Atencao>}
        para="/cotacoes"
      />
      <Kpi
        rotulo="Conversão em 90 dias"
        rotuloCurto="Conversão 90 dias"
        valor={{ tipo: "percentual", valor: 0.316 }}
        meta="6 de 19 cotações"
        para="/cotacoes"
      />
      <Kpi
        rotulo="Sinistros em aberto"
        valor={{ tipo: "inteiro", valor: 3 }}
        meta={<Atencao numero={1}>em análise</Atencao>}
        para="/sinistros"
      />
      <Kpi
        rotulo="Sinistralidade 12m"
        valor={{ tipo: "percentual", valor: 0.542 }}
        medidor={{ valor: 0.542, meta: 0.6 }}
      />
    </KpiCard>
  );
}

export function DemoCard() {
  return (
    <Card>
      <CardHeader
        titulo="Apólices recentes"
        subtitulo="Emitidas nos últimos 12 meses"
        nivel="h3"
        acoes={
          <Link
            to="/apolices"
            className="inline-flex items-center gap-1 text-label font-semibold text-link hover:underline"
          >
            Ver todas <ChevronRight size={13} aria-hidden />
          </Link>
        }
      />
      <CardBody className="pt-3">
        <ul className="divide-y divide-line-soft">
          {APOLICES.slice(0, 3).map((a) => (
            <li key={a.id} className="flex justify-between gap-3 py-3">
              <span>
                <span className="block text-item font-semibold text-ink">{a.segurado}</span>
                <span className="mono-id text-ink-3">{a.numero}</span>
              </span>
              <span className="text-right">
                <Money valor={a.premioTotal} className="block text-item font-semibold text-ink" />
                <span className="type-meta text-ink-3">
                  fim da vigência <DateText valor={a.vigenciaFim} />
                </span>
              </span>
            </li>
          ))}
        </ul>
      </CardBody>
      <CardFooter>
        <Button variante="secundario" larguraTotal>
          Ver todas as apólices
        </Button>
      </CardFooter>
    </Card>
  );
}

const COLUNAS: readonly ColunaTabela<ApoliceResumo>[] = [
  {
    id: "numero",
    cabecalho: "Apólice",
    codigo: true,
    celula: (a) => (
      <Link to={`/apolices/${a.id}`} className="text-link hover:underline">
        {a.numero}
      </Link>
    ),
  },
  {
    id: "segurado",
    cabecalho: "Segurado",
    ordenavel: true,
    celula: (a) => (
      <>
        <span className="block font-semibold">{a.segurado}</span>
        <span className="type-meta text-ink-3">CPF {a.cpfMascarado}</span>
      </>
    ),
  },
  { id: "produto", cabecalho: "Produto", celula: (a) => <ProdutoTag produto={a.produto} /> },
  {
    id: "vigencia",
    cabecalho: "Fim da vigência",
    numerica: true,
    ordenavel: true,
    celula: (a) => <DateText valor={a.vigenciaFim} />,
  },
  {
    id: "premio",
    cabecalho: "Prêmio total",
    numerica: true,
    ordenavel: true,
    celula: (a) => <Money valor={a.premioTotal} />,
  },
  {
    id: "status",
    cabecalho: "Situação",
    celula: (a) => <StatusBadge entidade="apolice" status={a.status} />,
  },
];

function ordenar(linhas: readonly ApoliceResumo[], ordem: Ordenacao | undefined) {
  if (!ordem) return linhas;
  const chave = (a: ApoliceResumo) =>
    ordem.coluna === "premio"
      ? a.premioTotal
      : ordem.coluna === "vigencia"
        ? a.vigenciaFim
        : a.segurado;
  const sinal = ordem.direcao === "asc" ? 1 : -1;
  return [...linhas].sort((a, b) => (chave(a) > chave(b) ? sinal : -sinal));
}

const FILTROS_STATUS: readonly StatusApolice[] = ["Ativa", "Programada", "Vencida", "Cancelada"];

function normalizarBusca(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

function filtrar(
  linhas: readonly ApoliceResumo[],
  status: ReadonlySet<StatusApolice>,
  termo: string,
) {
  const busca = normalizarBusca(termo);
  return linhas.filter(
    (a) =>
      (status.size === 0 || status.has(a.status)) &&
      (busca === "" || normalizarBusca(`${a.numero} ${a.segurado}`).includes(busca)),
  );
}

/** Densidade das tabelas na própria barra (a mesma preferência do menu de aparência). */
function BotaoDensidade() {
  const compacta = useDensidade() === "compacta";
  return (
    <IconButton
      icone={Rows3}
      rotulo="Modo compacto"
      comTooltip
      variante="secundario"
      aria-pressed={compacta}
      onClick={() => definirDensidade(compacta ? "confortavel" : "compacta")}
      className="aria-pressed:border-link aria-pressed:bg-surface-selected aria-pressed:text-ink max-md:size-11"
    />
  );
}

function BarraDaTabela({
  status,
  aoAlternarStatus,
  aoBuscar,
}: {
  status: ReadonlySet<StatusApolice>;
  aoAlternarStatus: (s: StatusApolice) => void;
  aoBuscar: (termo: string) => void;
}) {
  return (
    <BarraFerramentasTabela>
      <CampoBusca
        rotulo="Buscar por número ou segurado"
        placeholder="Número ou segurado"
        aoBuscar={aoBuscar}
      />
      <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Situação">
        {FILTROS_STATUS.map((s) => (
          <ChipFiltro
            key={s}
            ativo={status.has(s)}
            contador={APOLICES.filter((a) => a.status === s).length}
            onClick={() => aoAlternarStatus(s)}
          >
            {s}
          </ChipFiltro>
        ))}
      </div>
      <div className="flex items-center gap-2 md:ml-auto">
        <BotaoDensidade />
        <Button
          variante="secundario"
          tamanho="sm"
          icone={Download}
          onClick={() =>
            notificar.info({
              titulo: "Exportação",
              descricao: "O CSV da lista chega com a integração da API.",
            })
          }
        >
          Exportar
        </Button>
      </div>
    </BarraFerramentasTabela>
  );
}

export function DemoTabela() {
  const [ordem, setOrdem] = useState<Ordenacao | undefined>({ coluna: "premio", direcao: "desc" });
  const [modo, setModo] = useState<"dados" | "carregando" | "vazio">("dados");
  const [status, setStatus] = useState<ReadonlySet<StatusApolice>>(new Set());
  const [termo, setTermo] = useState("");
  // Trocar a key remonta a barra: a busca volta vazia ao limpar os filtros.
  const [limpezas, setLimpezas] = useState(0);
  const filtradas = filtrar(APOLICES, status, termo);
  const linhas = modo === "vazio" ? [] : ordenar(filtradas, ordem);

  function alternarStatus(s: StatusApolice) {
    setStatus((atual) => {
      const novo = new Set(atual);
      if (novo.has(s)) novo.delete(s);
      else novo.add(s);
      return novo;
    });
  }

  return (
    <Card>
      <CardHeader
        titulo="Apólices"
        subtitulo="Clique na linha ou no número; abaixo de 768 px vira lista de cards"
        nivel="h3"
        acoes={
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variante="secundario" tamanho="sm" iconeDireita={MoreHorizontal}>
                Estado: {modo}
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => setModo("dados")}>Com dados</DropdownMenuItem>
              <DropdownMenuItem onSelect={() => setModo("carregando")}>Carregando</DropdownMenuItem>
              <DropdownMenuItem onSelect={() => setModo("vazio")}>Sem resultado</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        }
      />
      <DataTable
        className="mt-4"
        legenda="Apólices da carteira"
        barraDeFerramentas={
          <BarraDaTabela
            key={limpezas}
            status={status}
            aoAlternarStatus={alternarStatus}
            aoBuscar={setTermo}
          />
        }
        colunas={COLUNAS}
        linhas={linhas}
        chave={(a) => a.id}
        hrefDaLinha={(a) => `/apolices/${a.id}`}
        selecionada={(a) => a.id === "4"}
        carregando={modo === "carregando"}
        ordenacao={ordem}
        aoOrdenar={(coluna) =>
          setOrdem((atual) => ({
            coluna,
            direcao: atual?.coluna === coluna && atual.direcao === "desc" ? "asc" : "desc",
          }))
        }
        cartao={(a) => (
          <span className="flex flex-col gap-1">
            <span className="flex items-center justify-between gap-2">
              <span className="mono-id text-label font-semibold text-link">{a.numero}</span>
              <StatusBadge entidade="apolice" status={a.status} />
            </span>
            <span className="text-sm font-semibold text-ink">{a.segurado}</span>
            <span className="flex flex-wrap items-center gap-x-3 type-meta text-ink-3">
              <ProdutoTag produto={a.produto} className="text-ink-2" />
              <Money valor={a.premioTotal} className="text-ink" />
              <span>
                até <DateText valor={a.vigenciaFim} />
              </span>
            </span>
          </span>
        )}
        vazio={
          <EmptyState
            icone={SearchX}
            nivel="h3"
            titulo={termo ? `Nada encontrado para “${termo}”` : "Nada encontrado"}
            descricao="Confira o número ou busque pelo nome do segurado."
            acao={
              <Button
                variante="secundario"
                onClick={() => {
                  setModo("dados");
                  setStatus(new Set());
                  setTermo("");
                  setLimpezas((n) => n + 1);
                }}
              >
                Limpar filtros
              </Button>
            }
          />
        }
      />
      {modo === "dados" && linhas.length > 0 && (
        <Pagination
          exibidos={linhas.length}
          total={12}
          temMais
          aoCarregarMais={() => undefined}
          substantivo="apólices"
        />
      )}
    </Card>
  );
}

const ETAPAS = [
  { id: "produto", titulo: "Produto", resumo: "Auto · pessoa física", href: "#produto" },
  { id: "segurado", titulo: "Segurado", resumo: "Lucas Andrade · 22 anos", href: "#segurado" },
  { id: "risco", titulo: "Risco", resumo: "T-Cross 2024 · 41820-021", href: "#risco" },
  { id: "coberturas", titulo: "Coberturas" },
  { id: "revisao", titulo: "Revisão", resumo: "Conferência e emissão" },
];

export function DemoStepper() {
  return (
    <div className="flex flex-col gap-3">
      <div className="flex justify-end">
        <StatusRascunho salvoAs="14:36" />
      </div>
      <Stepper etapas={ETAPAS} atual={3} />
    </div>
  );
}

export function DemoPainelCalculo() {
  const [parcelas, setParcelas] = useState(10);
  const [sheet, setSheet] = useState(false);
  const painel = (
    <PainelCalculo
      tarifa="Tarifa Auto"
      referencia={CALCULO_AUTO.referencia}
      atualizadoEm={HOJE}
      itens={ITENS_AUTO}
      fatores={FATORES_AUTO}
      premioLiquido={CALCULO_AUTO.premioLiquido}
      aliquotaIof={CALCULO_AUTO.aliquotaIof}
      iof={CALCULO_AUTO.iof}
      premioTotal={CALCULO_AUTO.premioTotal}
      maxParcelas={CALCULO_AUTO.maxParcelas}
      parcelas={parcelas}
      aoMudarParcelas={setParcelas}
      validaAte={CALCULO_AUTO.validaAte}
      acao={
        <Button larguraTotal tamanho="lg" iconeDireita={ChevronRight}>
          Revisar cotação
        </Button>
      }
      className="lg:static"
    />
  );
  return (
    <div className="flex flex-col gap-4">
      <div className="max-w-[376px]">{painel}</div>
      <div className="max-w-[390px]">
        <p className="mb-2 type-caption text-ink-3">Barra do total (abaixo de lg)</p>
        <BarraTotalMobile
          premioTotal={CALCULO_AUTO.premioTotal}
          aoVerCalculo={() => setSheet(true)}
          fixa={false}
        />
      </div>
      <Dialog aberto={sheet} aoMudar={setSheet} titulo="Memória de cálculo" largura="formulario">
        <PainelCalculo
          tarifa="Tarifa Auto"
          referencia={CALCULO_AUTO.referencia}
          itens={ITENS_AUTO}
          fatores={FATORES_AUTO}
          premioLiquido={CALCULO_AUTO.premioLiquido}
          aliquotaIof={CALCULO_AUTO.aliquotaIof}
          iof={CALCULO_AUTO.iof}
          premioTotal={CALCULO_AUTO.premioTotal}
          maxParcelas={CALCULO_AUTO.maxParcelas}
          parcelas={parcelas}
          aoMudarParcelas={setParcelas}
          validaAte={CALCULO_AUTO.validaAte}
          estado="desatualizado"
          className="border-0 lg:static"
        />
      </Dialog>
    </div>
  );
}

function LinkId({ para, children }: { para: string; children: string }) {
  return (
    <Link to={para} className="mono-id text-link hover:underline hover:underline-offset-[3px]">
      {children}
    </Link>
  );
}

export function DemoTimeline() {
  return (
    <Card>
      <CardHeader titulo="Linha do tempo" subtitulo="Eventos registrados na apólice" nivel="h3" />
      <CardBody className="pt-4">
        <Timeline
          eventos={EVENTOS_APOLICE.map((e) => {
            // IDs no título/descrição em mono-id e como link (DESIGN.md §4.14).
            if (e.id === "sinistro") {
              return {
                ...e,
                titulo: (
                  <>
                    Sinistro <LinkId para="/sinistros/97">SIN-2026-000097</LinkId> aberto
                  </>
                ),
                extra: <StatusBadge entidade="sinistro" status="EmAnalise" />,
              };
            }
            if (e.id === "emissao") {
              return {
                ...e,
                descricao: (
                  <>
                    A partir da cotação <LinkId para="/cotacoes/1187">COT-2026-001187</LinkId>, por
                    Ana Ribeiro.
                  </>
                ),
              };
            }
            return e;
          })}
          hoje={HOJE}
        />
      </CardBody>
    </Card>
  );
}

export function DemoTabs() {
  return (
    <Tabs
      rotulo="Seções da apólice"
      abas={[
        {
          valor: "geral",
          rotulo: "Visão geral",
          conteudo: <p className="text-sm text-ink-2">Resumo da apólice e do risco.</p>,
        },
        {
          valor: "coberturas",
          rotulo: "Coberturas",
          contador: 6,
          conteudo: <p className="text-sm text-ink-2">Tabela de coberturas com totais.</p>,
        },
        {
          valor: "parcelas",
          rotulo: "Parcelas",
          contador: 10,
          conteudo: <p className="text-sm text-ink-2">1ª parcela absorve o ajuste de centavos.</p>,
        },
        {
          valor: "endossos",
          rotulo: "Endossos",
          contador: 1,
          conteudo: <p className="text-sm text-ink-2">Endosso nº 1.</p>,
        },
        {
          valor: "sinistros",
          rotulo: "Sinistros",
          contador: 1,
          conteudo: <p className="text-sm text-ink-2">SIN-2026-000097.</p>,
        },
        {
          valor: "auditoria",
          rotulo: "Auditoria",
          conteudo: <p className="text-sm text-ink-2">Trilha completa.</p>,
        },
      ]}
    />
  );
}

const MOTIVOS = [
  {
    valor: "PedidoDoSegurado",
    rotulo: "Pedido do segurado",
    detalhe: "Prazo curto: retém 20% (30 dias decorridos)",
  },
  { valor: "IniciativaSeguradora", rotulo: "Iniciativa da seguradora", detalhe: "Pro rata die" },
  { valor: "Inadimplencia", rotulo: "Inadimplência", detalhe: "Pro rata die" },
] as const;

type Motivo = (typeof MOTIVOS)[number]["valor"];

export function DemoSobreposicoes() {
  const [motivo, setMotivo] = useState<Motivo | "">("");
  return (
    <div className="flex flex-wrap items-center gap-2">
      <ConfirmDialog
        gatilho={
          <Button variante="perigo-contorno" icone={Ban}>
            Cancelar apólice
          </Button>
        }
        titulo="Cancelar AUTO-2026-000042"
        descricao="Escolha o motivo para simular a restituição. O IOF não é restituído."
        rotuloConfirmar="Cancelar apólice"
        rotuloConfirmando="Cancelando…"
        confirmarDesabilitado={motivo === ""}
        largura="formulario"
        aoConfirmar={() =>
          new Promise<void>((ok) =>
            window.setTimeout(() => {
              notificar.sucesso({
                titulo: "Apólice cancelada",
                descricao: "Restituição de R$ 3.622,56 registrada.",
              });
              ok();
            }, 900),
          )
        }
      >
        <GrupoCartoesEscolha
          rotulo="Motivo do cancelamento"
          opcoes={MOTIVOS.map((m) => ({ valor: m.valor, titulo: m.rotulo, descricao: m.detalhe }))}
          valor={motivo}
          aoMudar={setMotivo}
        />
        <div className="mt-4 rounded-lg bg-surface-2 px-4 py-3">
          <p className="type-meta text-ink-3">Restituição estimada</p>
          <Money
            valor={motivo === "" ? 0 : 3622.56}
            variante="heroi"
            className="type-fig-hero text-ink"
          />
        </div>
      </ConfirmDialog>

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variante="secundario" iconeDireita={MoreHorizontal}>
            Ações
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem icone={FilePen}>Endossar</DropdownMenuItem>
          <DropdownMenuItem icone={Copy}>Copiar número</DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem icone={Ban} perigo>
            Cancelar apólice
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <Tooltip conteudo="Restrito ao perfil Admin">
        <Button variante="fantasma" icone={Lock}>
          Tarifas
        </Button>
      </Tooltip>

      <Button
        variante="secundario"
        onClick={() =>
          notificar.sucesso({
            titulo: "Apólice emitida",
            descricao: "AUTO-2026-000051 para Marcelo Tavares.",
            acao: { rotulo: "Ver apólice", aoClicar: () => undefined },
          })
        }
      >
        Toast sucesso
      </Button>
      <Button
        variante="secundario"
        onClick={() =>
          notificar.alerta({
            titulo: "Cotação expira em 48 h",
            descricao: "COT-2026-004811 · Juliana Prado",
          })
        }
      >
        Toast alerta
      </Button>
      <Button
        variante="secundario"
        onClick={() =>
          notificar.erro({
            titulo: "Não foi possível emitir a apólice",
            descricao: "A cotação expirou. Recalcule e tente de novo.",
            correlationId: "7f3c9a2e-41b8",
          })
        }
      >
        Toast erro
      </Button>
    </div>
  );
}

export function DemoEstados() {
  return (
    <div className="grid gap-4">
      <Card>
        <EmptyState
          icone={Inbox}
          nivel="h3"
          titulo="Nenhuma cotação ainda"
          descricao="Comece pela primeira cotação: o prêmio é calculado ao vivo, com a memória de cálculo ao lado."
          acao={<Button icone={Plus}>Nova cotação</Button>}
          acaoSecundaria={
            <Link to="/sobre" className="text-label font-semibold text-link hover:underline">
              Como funciona
            </Link>
          }
        />
      </Card>
      <Card>
        <EmptyState
          icone={Lock}
          nivel="h3"
          titulo="Disponível para o perfil Admin"
          descricao="Troque para a persona Diego Martins."
        />
      </Card>
      <Card>
        <ErrorState
          nivel="h3"
          titulo="Não foi possível carregar as apólices"
          correlationId="7f3c9a2e-41b8"
          aoTentarDeNovo={() => undefined}
        />
      </Card>
      <Card>
        <CardBody className="flex flex-col gap-4">
          <p className="type-caption text-ink-3">Skeleton (aparece após 200 ms)</p>
          <div className="flex items-center gap-3">
            <Skeleton className="size-9 rounded-full" />
            <SkeletonTexto linhas={2} className="flex-1" />
          </div>
          <Skeleton className="h-9 w-40" />
        </CardBody>
      </Card>
      <Breadcrumbs
        itens={[
          { rotulo: "Apólices", href: "/apolices" },
          { rotulo: "Auto", href: "/apolices?produto=AUTO" },
          { rotulo: "AUTO-2026-000042", codigo: true },
        ]}
      />
    </div>
  );
}

const DISTRIBUICAO = [
  { produto: "AUTO" as const, valor: 32841.1, apolices: 6 },
  { produto: "RESID" as const, valor: 8210.4, apolices: 3 },
  { produto: "VIDA" as const, valor: 6640, apolices: 2 },
];

export function DemoGraficos() {
  const total = DISTRIBUICAO.reduce((s, d) => s + d.valor, 0);
  return (
    <div className="grid gap-4 lg:grid-cols-12">
      <Card className="lg:col-span-8">
        <CardHeader
          titulo="Prêmio emitido"
          subtitulo="Últimos 12 meses · outubro parcial"
          nivel="h3"
          // Total na mesma linha do título, à direita, como na referência mobile.
          className="flex-nowrap"
          acoes={
            <div className="text-right">
              {/* Formato de KPI (R$ e mil na linha de base), não de herói (DESIGN.md §3.8/§4.15). */}
              <NumeroKpi valor={{ tipo: "moeda", valor: 47688.86 }} tamanho="total" />
              <p className="mt-1 type-meta text-ink-3">
                <Delta valor={0.142} formato="percentual">
                  <span className="max-md:sr-only">vs. 12 meses anteriores</span>
                </Delta>
              </p>
            </div>
          }
        />
        <CardBody>
          <GraficoBarras
            dados={PREMIO_MENSAL}
            rotulo="Prêmio emitido por mês, de novembro de 2025 a outubro de 2026"
            mesCorrente="2026-10"
            parcialAte="2026-10-21"
            media={MEDIA_MENSAL}
          />
        </CardBody>
      </Card>
      <Card className="lg:col-span-4">
        <CardHeader
          titulo="Distribuição por produto"
          subtitulo="Prêmio emitido em 12 meses"
          nivel="h3"
        />
        <CardBody>
          <BarraDistribuicao fatias={DISTRIBUICAO} />
          <ul className="mt-2 divide-y divide-line-soft">
            {DISTRIBUICAO.map((d) => (
              <li key={d.produto} className="flex items-center justify-between gap-3 py-3.5">
                <span>
                  <ProdutoTag produto={d.produto} className="font-semibold" />
                  <span className="block pl-[18px] type-meta text-ink-3 tnum">
                    {d.apolices} apólices · {Math.round((d.valor / total) * 100)}%
                  </span>
                </span>
                <NumeroKpi valor={{ tipo: "moeda", valor: d.valor }} tamanho="sm" />
              </li>
            ))}
          </ul>
          {/* Mobile: uma linha por produto (três colunas de R$ 5.474,00 não cabem em 390 px). */}
          <dl className="mt-2 grid gap-2 border-t border-line pt-3.5 sm:grid-cols-3">
            {DISTRIBUICAO.map((d) => (
              <div key={d.produto} className="flex items-baseline justify-between gap-3 sm:block">
                <dt className="text-xs text-ink-3">
                  Ticket {d.produto === "RESID" ? "Resid." : d.produto === "AUTO" ? "Auto" : "Vida"}
                </dt>
                <dd className="text-sm font-semibold text-ink tnum">
                  {formatarMoeda(Math.round(d.valor / d.apolices))}
                </dd>
              </div>
            ))}
          </dl>
        </CardBody>
      </Card>
    </div>
  );
}

import { ArrowLeft } from "lucide-react";
import { useRef, type ComponentType } from "react";
import { Link } from "react-router";

import { useAnuncioDeRota } from "@/app/anuncio-de-rota";
import { ThemeToggle } from "@/ui/componentes/ThemeToggle";
import { EscopoTema } from "@/ui/tema/EscopoTema";
import { Marca } from "@/ui/marca/Marca";
import { cn } from "@/ui/utils/cn";

import { DemoBotoes, DemoFormulario, DemoMarca, DemoStatus, DemoTipografia } from "./DemosBase";
import {
  DemoCard,
  DemoEstados,
  DemoGraficos,
  DemoKpis,
  DemoPainelCalculo,
  DemoSobreposicoes,
  DemoStepper,
  DemoTabela,
  DemoTabs,
  DemoTimeline,
} from "./DemosComplexos";

interface Secao {
  id: string;
  titulo: string;
  descricao: string;
  Demo: ComponentType;
  /** Componentes largos ficam um tema embaixo do outro, em largura total. */
  empilhado?: boolean;
}

const SECOES: readonly Secao[] = [
  {
    id: "tipografia",
    titulo: "Tipografia e números",
    descricao: "Serifa nos números que importam, sans tabular no resto, mono nos IDs.",
    Demo: DemoTipografia,
  },
  {
    id: "marca",
    titulo: "Marca, gravura e superfície oficial",
    descricao: "Marinho com fio dourado; botões e badges sobre marinho.",
    Demo: DemoMarca,
  },
  {
    id: "botoes",
    titulo: "Botões",
    descricao: "Primário, secundário, fantasma, perigo; tamanhos, carregando e ícone.",
    Demo: DemoBotoes,
  },
  {
    id: "formulario",
    titulo: "Formulário",
    descricao:
      "FormField, Input, Select, Switch, SegmentedControl, Checkbox, Textarea (RHF + Zod).",
    Demo: DemoFormulario,
    empilhado: true,
  },
  {
    id: "status",
    titulo: "Status, produtos e avatares",
    descricao: "Estado nunca só por cor: ponto ou ícone + rótulo.",
    Demo: DemoStatus,
  },
  {
    id: "kpis",
    titulo: "Faixa de KPIs",
    descricao: "2 colunas no mobile, 3 no md, 6 no xl; cada KPI é link.",
    Demo: DemoKpis,
    empilhado: true,
  },
  { id: "card", titulo: "Card", descricao: "Cabeçalho em serifa, ações e rodapé.", Demo: DemoCard },
  {
    id: "tabela",
    titulo: "Tabela e paginação",
    descricao: "Ordenação, linha selecionada, estados vazio e carregando, cards no mobile.",
    Demo: DemoTabela,
    empilhado: true,
  },
  {
    id: "stepper",
    titulo: "Stepper",
    descricao: "Assistente de cotação em 5 etapas, com autosave.",
    Demo: DemoStepper,
    empilhado: true,
  },
  {
    id: "calculo",
    titulo: "Painel de cálculo",
    descricao: "Memória de cálculo do SPEC §4.2: itens, fatores, líquido, IOF, total e parcelas.",
    Demo: DemoPainelCalculo,
  },
  {
    id: "timeline",
    titulo: "Linha do tempo",
    descricao: "Mais recente no topo, com marcador Hoje.",
    Demo: DemoTimeline,
  },
  {
    id: "abas",
    titulo: "Abas",
    descricao: "Contadores, setas e sublinhado que desliza.",
    Demo: DemoTabs,
  },
  {
    id: "sobreposicoes",
    titulo: "Diálogos, menus, tooltip e toasts",
    descricao: "Foco preso, Esc fecha, foco inicial na ação segura.",
    Demo: DemoSobreposicoes,
  },
  {
    id: "estados",
    titulo: "Estados vazio, erro e carregando",
    descricao: "Primeiro uso, sem permissão, erro com ID de correlação, skeleton.",
    Demo: DemoEstados,
  },
  {
    id: "graficos",
    titulo: "Gráficos",
    descricao: "Barras com projeção, média e alternativa em tabela; distribuição por produto.",
    Demo: DemoGraficos,
    empilhado: true,
  },
];

function DoisTemas({ Demo, empilhado = false }: { Demo: ComponentType; empilhado?: boolean }) {
  return (
    <div className={cn("grid gap-4", !empilhado && "xl:grid-cols-2")}>
      {(["claro", "escuro"] as const).map((tema) => (
        <EscopoTema
          key={tema}
          tema={tema}
          className="min-w-0 rounded-2xl border border-line p-4 md:p-6"
        >
          <p className="mb-4 type-caption text-ink-3">
            {tema === "claro" ? "Tema claro" : "Tema escuro"}
          </p>
          <Demo />
        </EscopoTema>
      ))}
    </div>
  );
}

/** Vitrine de revisão visual (só em desenvolvimento): todos os componentes nos dois temas. */
export default function Vitrine() {
  const conteudo = useRef<HTMLElement>(null);
  useAnuncioDeRota("Vitrine de componentes", conteudo);

  return (
    <div className="min-h-dvh bg-canvas">
      <header className="sticky top-0 z-30 border-b border-line bg-surface">
        <div className="mx-auto flex h-16 max-w-[1440px] items-center gap-3 px-4 md:px-10">
          <Link
            to="/"
            aria-label="Voltar ao app"
            className="grid size-10 place-items-center rounded-md text-ink-2 hover:bg-hover-tint"
          >
            <ArrowLeft size={18} aria-hidden />
          </Link>
          <Marca tamanhoSelo={26} classePalavra="text-xl text-ink" />
          <span className="hidden rounded-full border border-demo-line bg-demo-bg px-2.5 py-0.5 text-meta font-semibold text-demo sm:inline">
            Só em desenvolvimento
          </span>
          <div className="ml-auto">
            <ThemeToggle comDensidade />
          </div>
        </div>
      </header>

      <main
        ref={conteudo}
        id="conteudo"
        className="mx-auto flex max-w-[1440px] flex-col gap-10 px-4 py-8 md:px-10"
      >
        <div>
          <h1 tabIndex={-1} className="type-title-page text-ink outline-none">
            Vitrine de componentes
          </h1>
          <p className="mt-1.5 max-w-3xl text-sm text-ink-3">
            Design system do Apólice (docs/design/DESIGN.md) com dados fictícios realistas. Cada
            bloco aparece no tema claro e no escuro; o seletor no topo troca o tema da página.
          </p>
          <nav aria-label="Seções da vitrine" className="mt-4">
            <ul className="flex flex-wrap gap-2">
              {SECOES.map((s) => (
                <li key={s.id}>
                  <a
                    href={`#${s.id}`}
                    className="inline-flex h-8 items-center rounded-full border border-line bg-surface px-3 text-label text-ink-2 hover:text-ink"
                  >
                    {s.titulo}
                  </a>
                </li>
              ))}
            </ul>
          </nav>
        </div>

        {SECOES.map((secao) => (
          <section
            key={secao.id}
            id={secao.id}
            aria-labelledby={`${secao.id}-titulo`}
            className="scroll-mt-20"
          >
            <h2 id={`${secao.id}-titulo`} className="type-title-card text-ink">
              {secao.titulo}
            </h2>
            <p className="mt-1 mb-4 text-label text-ink-3">{secao.descricao}</p>
            <DoisTemas Demo={secao.Demo} empilhado={secao.empilhado ?? false} />
          </section>
        ))}
      </main>
    </div>
  );
}

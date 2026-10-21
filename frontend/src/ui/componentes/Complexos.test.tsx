import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";

import { axe, renderComRotas, renderSimples } from "@/test/utilitarios";

import { DataTable, type ColunaTabela } from "./DataTable";
import { BarraTotalMobile, PainelCalculo, type EstadoCalculo } from "./PainelCalculo";
import { Stepper } from "./Stepper";
import { Tabs } from "./Tabs";
import { Timeline } from "./Timeline";

interface Linha {
  id: string;
  numero: string;
  segurado: string;
  premio: number;
}

const LINHAS: Linha[] = [
  { id: "1", numero: "AUTO-2026-000042", segurado: "Rafael Nogueira Castro", premio: 4862.38 },
  { id: "2", numero: "VIDA-2025-000063", segurado: "Helena Duarte", premio: 2640 },
];

const COLUNAS: ColunaTabela<Linha>[] = [
  {
    id: "numero",
    cabecalho: "Apólice",
    codigo: true,
    celula: (l) => <a href={`/apolices/${l.id}`}>{l.numero}</a>,
  },
  { id: "segurado", cabecalho: "Segurado", celula: (l) => l.segurado },
  {
    id: "premio",
    cabecalho: "Prêmio total",
    numerica: true,
    ordenavel: true,
    celula: (l) => String(l.premio),
  },
];

describe("DataTable", () => {
  it("deve ter caption, cabeçalhos com scope e números alinhados à direita", () => {
    renderComRotas(
      <DataTable
        legenda="Apólices da carteira"
        colunas={COLUNAS}
        linhas={LINHAS}
        chave={(l) => l.id}
      />,
    );
    const tabela = screen.getByRole("table", { name: "Apólices da carteira" });
    expect(within(tabela).getByRole("columnheader", { name: "Segurado" })).toHaveAttribute(
      "scope",
      "col",
    );
    const celulaValor = within(tabela).getByRole("cell", { name: "4862.38" });
    expect(celulaValor).toHaveClass("text-right", "tnum");
  });

  it("deve navegar ao clicar na linha, mantendo o link como alvo acessível", async () => {
    const user = userEvent.setup();
    const { router } = renderComRotas(
      <DataTable
        legenda="Apólices"
        colunas={COLUNAS}
        linhas={LINHAS}
        chave={(l) => l.id}
        hrefDaLinha={(l) => `/apolices/${l.id}`}
      />,
      { rotasExtras: [{ path: "/apolices/:id", element: <p>Detalhe</p> }] },
    );
    const tabela = screen.getByRole("table");
    expect(within(tabela).getByRole("link", { name: "AUTO-2026-000042" })).toBeInTheDocument();
    await user.click(within(tabela).getByRole("cell", { name: "Helena Duarte" }));
    expect(router.state.location.pathname).toBe("/apolices/2");
  });

  it("deve ordenar pelo botão do cabeçalho e expor aria-sort", async () => {
    const user = userEvent.setup();
    const aoOrdenar = vi.fn();
    renderComRotas(
      <DataTable
        legenda="Apólices"
        colunas={COLUNAS}
        linhas={LINHAS}
        chave={(l) => l.id}
        ordenacao={{ coluna: "premio", direcao: "desc" }}
        aoOrdenar={aoOrdenar}
      />,
    );
    const cabecalho = screen.getByRole("columnheader", { name: /Prêmio total/ });
    expect(cabecalho).toHaveAttribute("aria-sort", "descending");
    within(cabecalho).getByRole("button").focus();
    await user.keyboard("{Enter}");
    expect(aoOrdenar).toHaveBeenCalledWith("premio");
  });

  it("deve mostrar o estado vazio e o carregamento", () => {
    const { rerender } = renderComRotas(
      <DataTable
        legenda="Apólices"
        colunas={COLUNAS}
        linhas={[]}
        chave={(l) => l.id}
        vazio={<p>Nada encontrado</p>}
      />,
    );
    expect(screen.getByText("Nada encontrado")).toBeInTheDocument();
    rerender(<></>);
    renderComRotas(
      <DataTable legenda="Apólices" colunas={COLUNAS} linhas={[]} chave={(l) => l.id} carregando />,
    );
    expect(screen.getByText("Carregando…")).toHaveClass("sr-only");
  });

  it("deve manter a barra de ferramentas visível também sem resultado", () => {
    renderComRotas(
      <DataTable
        legenda="Apólices"
        colunas={COLUNAS}
        linhas={[]}
        chave={(l) => l.id}
        barraDeFerramentas={<button type="button">Limpar filtros</button>}
        vazio={<p>Nada encontrado</p>}
      />,
    );
    expect(screen.getByRole("button", { name: "Limpar filtros" })).toBeInTheDocument();
    expect(screen.getByText("Nada encontrado")).toBeInTheDocument();
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderComRotas(
      <DataTable
        legenda="Apólices"
        colunas={COLUNAS}
        linhas={LINHAS}
        chave={(l) => l.id}
        selecionada={(l) => l.id === "1"}
        aoOrdenar={vi.fn()}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

const ETAPAS = [
  { id: "produto", titulo: "Produto", resumo: "Auto", href: "/cotacoes/nova/produto" },
  {
    id: "segurado",
    titulo: "Segurado",
    resumo: "Lucas Andrade · 22 anos",
    href: "/cotacoes/nova/segurado",
  },
  { id: "risco", titulo: "Risco" },
  { id: "coberturas", titulo: "Coberturas" },
  { id: "revisao", titulo: "Revisão" },
];

describe("Stepper", () => {
  it("deve marcar a etapa atual e só deixar as concluídas navegáveis", async () => {
    const { container } = renderComRotas(<Stepper etapas={ETAPAS} atual={2} />);
    const lista = container.querySelector("ol");
    expect(lista).not.toBeNull();
    const itens = within(lista as HTMLElement).getAllByRole("listitem");
    expect(itens[2]).toHaveAttribute("aria-current", "step");
    expect(within(lista as HTMLElement).getAllByRole("link")).toHaveLength(2);
    expect(within(lista as HTMLElement).getByRole("link", { name: /Segurado/ })).toHaveAttribute(
      "href",
      "/cotacoes/nova/segurado",
    );
    expect(screen.getByText("Etapa 3 de 5")).toBeInTheDocument();
    expect(await axe(container)).toHaveNoViolations();
  });
});

function PainelDeTeste({
  premioTotal = 3312.05,
  estado,
  aoTentarDeNovo,
  parcelasIniciais = 10,
}: {
  premioTotal?: number;
  estado?: EstadoCalculo;
  aoTentarDeNovo?: () => void;
  parcelasIniciais?: number;
}) {
  const [parcelas, setParcelas] = useState(parcelasIniciais);
  return (
    <PainelCalculo
      tarifa="Tarifa Auto"
      referencia="2026-10-01"
      itens={[
        { codigo: "CASCO", nome: "Casco", premio: 2986.42 },
        { codigo: "ASSIST", nome: "Assistência 24 h", premio: 98, fixo: true },
      ]}
      fatores={[
        { id: "idade", descricao: "Condutor de 18 a 25 anos", fator: 1.4 },
        { id: "bonus", descricao: "Bônus classe 3", fator: 0.9 },
      ]}
      premioLiquido={3084.42}
      aliquotaIof={0.0738}
      iof={227.63}
      premioTotal={premioTotal}
      maxParcelas={10}
      parcelas={parcelas}
      aoMudarParcelas={setParcelas}
      validaAte="2026-11-20"
      {...(estado ? { estado } : {})}
      {...(aoTentarDeNovo ? { aoTentarDeNovo } : {})}
    />
  );
}

describe("PainelCalculo", () => {
  it("deve mostrar a memória de cálculo com IOF do DTO e fatores explicados", () => {
    renderSimples(<PainelDeTeste />);
    expect(screen.getByText("IOF 7,38%")).toBeInTheDocument();
    expect(screen.getByText("fixo")).toBeInTheDocument();
    expect(screen.getByText("agrava")).toHaveClass("sr-only");
    expect(screen.getByText("desconta")).toHaveClass("sr-only");
    expect(screen.getByText(/válida até 20\/11\/2026/)).toBeInTheDocument();
    // 3.312,05 em 10×: base 331,20 e a 1ª absorve os centavos (331,25).
    expect(screen.getByText(/1ª de R\$ 331,25 \+ 9× de R\$ 331,20/)).toBeInTheDocument();
  });

  it("deve ter só dt e dd em cada grupo da memória de cálculo (HTML válido)", () => {
    const { container } = renderSimples(<PainelDeTeste />);
    const grupos = container.querySelectorAll("dl > div");
    expect(grupos.length).toBeGreaterThan(0);
    for (const grupo of grupos) {
      const tags = [...grupo.children].map((filho) => filho.tagName);
      expect(tags).toEqual(["DT", "DD"]);
    }
  });

  it("deve trocar o parcelamento pelo teclado", async () => {
    const user = userEvent.setup();
    renderSimples(<PainelDeTeste />);
    screen.getByRole("radio", { name: "10×" }).focus();
    await user.keyboard("{ArrowLeft>}");
    await waitFor(() => expect(screen.getByRole("radio", { name: "6×" })).toBeChecked());
    await user.keyboard("{/ArrowLeft}");
    expect(screen.getByText(/5× de R\$ 552,00/)).toBeInTheDocument();
  });

  it('deve abrir a lista completa em "Outro" e marcá-lo para parcelas fora dos atalhos', async () => {
    const user = userEvent.setup();
    const { unmount } = renderSimples(<PainelDeTeste />);
    expect(screen.queryByRole("combobox", { name: "Número de parcelas" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("radio", { name: "Outro" }));
    expect(screen.getByRole("combobox", { name: "Número de parcelas" })).toBeInTheDocument();
    unmount();
    renderSimples(<PainelDeTeste parcelasIniciais={4} />);
    expect(screen.getByRole("radio", { name: "Outro" })).toBeChecked();
    expect(screen.getByRole("combobox", { name: "Número de parcelas" })).toHaveTextContent("4×");
  });

  it("deve anunciar só o total, com atraso, quando ele muda", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    function Simulacao() {
      const [total, setTotal] = useState(3312.05);
      return (
        <>
          <button type="button" onClick={() => setTotal(4581.08)}>
            simular
          </button>
          <PainelDeTeste premioTotal={total} />
        </>
      );
    }
    renderSimples(<Simulacao />);
    const regiao = document.querySelector('[data-anunciador="polite"]');
    expect(regiao?.textContent ?? "").not.toMatch(/Prêmio total/);
    act(() => screen.getByRole("button", { name: "simular" }).click());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(document.querySelector('[data-anunciador="polite"]')).toHaveTextContent(
      "Prêmio total atualizado: R$ 4.581,08",
    );
    vi.useRealTimers();
  });

  it("deve manter o último valor e oferecer tentar de novo quando desatualizado", async () => {
    const user = userEvent.setup();
    const aoTentarDeNovo = vi.fn();
    renderSimples(<PainelDeTeste estado="desatualizado" aoTentarDeNovo={aoTentarDeNovo} />);
    expect(screen.getByText("R$ 3.312,05")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Desatualizado · Tentar de novo" }));
    expect(aoTentarDeNovo).toHaveBeenCalledOnce();
  });

  it("deve marcar aria-busy e esmaecer só os valores durante o recálculo", () => {
    const { container } = renderSimples(<PainelDeTeste estado="recalculando" />);
    const painel = screen.getByRole("region", { name: "Cálculo do prêmio" });
    expect(painel).toHaveAttribute("aria-busy", "true");
    // Rótulos e notas não esmaecem (cairiam abaixo de 4,5:1); só os valores em --ink.
    expect(screen.getByText("fixo")).not.toHaveClass("opacity-60");
    expect(container.querySelectorAll("dd .opacity-60").length).toBeGreaterThan(0);
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(<PainelDeTeste />);
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("BarraTotalMobile", () => {
  it("deve abrir o cálculo e não ter violações", async () => {
    const user = userEvent.setup();
    const aoVerCalculo = vi.fn();
    const { container } = renderSimples(
      <BarraTotalMobile premioTotal={4482.93} aoVerCalculo={aoVerCalculo} fixa={false} />,
    );
    expect(screen.getByText("R$ 4.482,93")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Ver cálculo" }));
    expect(aoVerCalculo).toHaveBeenCalledOnce();
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Timeline", () => {
  it("deve ordenar do mais recente e inserir o marcador Hoje entre futuro e passado", async () => {
    const { container } = renderSimples(
      <Timeline
        hoje={new Date("2026-10-21T14:00:00-03:00")}
        eventos={[
          { id: "a", quando: "2026-03-15T09:12:00-03:00", titulo: "Apólice emitida", tom: "navy" },
          { id: "b", quando: "2026-11-15", titulo: "Vencimento da parcela 9/10", tom: "steel" },
          { id: "c", quando: "2026-10-21T11:47:00-03:00", titulo: "Sinistro aberto", tom: "warn" },
        ]}
      />,
    );
    const itens = screen.getAllByRole("listitem");
    expect(
      itens.map((i) => within(i).getByText(/Apólice|Vencimento|Sinistro/).textContent),
    ).toEqual(["Vencimento da parcela 9/10", "Sinistro aberto", "Apólice emitida"]);
    expect(within(itens[1]!).getByText("Hoje")).toBeInTheDocument();
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Tabs", () => {
  it("deve navegar pelas abas com as setas e mostrar o painel ativo", async () => {
    const user = userEvent.setup();
    const { container } = renderSimples(
      <Tabs
        rotulo="Seções da apólice"
        abas={[
          { valor: "geral", rotulo: "Visão geral", conteudo: <p>Resumo</p> },
          {
            valor: "coberturas",
            rotulo: "Coberturas",
            contador: 6,
            conteudo: <p>Tabela de coberturas</p>,
          },
          { valor: "parcelas", rotulo: "Parcelas", contador: 10, conteudo: <p>Cronograma</p> },
        ]}
      />,
    );
    expect(screen.getByRole("tablist", { name: "Seções da apólice" })).toBeInTheDocument();
    await user.tab();
    expect(screen.getByRole("tab", { name: "Visão geral" })).toHaveFocus();
    await user.keyboard("{ArrowRight}");
    const coberturas = screen.getByRole("tab", { name: /Coberturas/ });
    expect(coberturas).toHaveFocus();
    expect(coberturas).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel")).toHaveTextContent("Tabela de coberturas");
    await user.keyboard("{End}");
    expect(screen.getByRole("tab", { name: /Parcelas/ })).toHaveFocus();
    expect(await axe(container)).toHaveNoViolations();
  });
});

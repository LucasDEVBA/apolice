import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Inbox } from "lucide-react";
import { describe, expect, it, vi } from "vitest";

import { axe, renderComRotas, renderSimples } from "@/test/utilitarios";

import { Avatar } from "./Avatar";
import { Badge, StatusBadge } from "./Badge";
import { Breadcrumbs } from "./Breadcrumbs";
import { EmptyState } from "./EmptyState";
import { ErrorState } from "./ErrorState";
import { Atencao, Delta, Kpi, KpiCard, NumeroKpi } from "./KpiCard";
import { Pagination } from "./Pagination";
import { ProdutoTag } from "./ProdutoTag";
import { DateText, Money, Percent } from "./Valores";

// O Testing Library normaliza espaços (o NBSP do Intl vira espaço comum) nas consultas por texto.
const ESP = " ";

describe("StatusBadge", () => {
  it("deve mostrar o rótulo de negócio com marcador, nunca só cor", () => {
    renderSimples(
      <>
        <StatusBadge entidade="sinistro" status="EmAnalise" />
        <StatusBadge entidade="sinistro" status="Pago" />
        <StatusBadge entidade="cotacao" status="Expirada" />
        <StatusBadge entidade="apolice" status="Cancelada" />
      </>,
    );
    expect(screen.getByText("Em análise")).toHaveClass("text-warn");
    expect(screen.getByText("Pago").querySelector("svg")).not.toBeNull();
    expect(screen.getByText("Expirada")).toHaveClass("bg-warn-bg");
    expect(screen.getByText("Cancelada")).toHaveClass("text-bad");
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <>
        <StatusBadge entidade="apolice" status="Ativa" />
        <StatusBadge entidade="apolice" status="Ativa" sobreMarinho />
        <Badge tom="neutral" marcador="plain">
          Obrigatória
        </Badge>
        <ProdutoTag produto="AUTO" />
        <ProdutoTag produto="VIDA" variante="chip" />
        <Avatar nome="Ana Ribeiro" rotulado />
      </>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("ProdutoTag e Avatar", () => {
  it("deve dar nome ao chip de produto e iniciais ao avatar", () => {
    renderSimples(
      <>
        <ProdutoTag produto="RESID" variante="chip" />
        <Avatar nome="Carla Souza" rotulado />
        <Avatar nome="Bruno Lima" />
      </>,
    );
    expect(screen.getByText("Residencial")).toHaveClass("sr-only");
    expect(screen.getByRole("img", { name: "Carla Souza" })).toHaveTextContent("CS");
    expect(screen.getByText("BL")).toHaveAttribute("aria-hidden", "true");
  });

  it("não devem ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <div>
        <ProdutoTag produto="AUTO" />
        <ProdutoTag produto="VIDA" variante="chip" />
        <Avatar nome="Diego Martins" variante="marinho" anel rotulado />
        <Avatar nome="Ana Ribeiro" />
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Valores", () => {
  it("deve formatar moeda, data e percentual em pt-BR", () => {
    renderSimples(
      <>
        <Money valor={1234.56} data-testid="moeda" />
        <Money valor={-379.21} data-testid="negativo" />
        <DateText valor="2026-10-21" />
        <Percent valor={0.0738} />
      </>,
    );
    expect(screen.getByTestId("moeda")).toHaveTextContent(`R$${ESP}1.234,56`);
    expect(screen.getByTestId("negativo")).toHaveTextContent(`−R$${ESP}379,21`);
    expect(screen.getByText("21/10/2026")).toHaveAttribute("dateTime", "2026-10-21");
    expect(screen.getByText("7,38%")).toBeInTheDocument();
  });

  it("deve entregar o herói inteiro ao leitor de tela e esconder as partes visuais", () => {
    const { container } = renderSimples(<Money valor={4862.38} variante="heroi" />);
    expect(screen.getByText(`R$${ESP}4.862,38`)).toHaveClass("sr-only");
    const partesVisuais = container.querySelector('[aria-hidden="true"]');
    expect(partesVisuais).toHaveTextContent("R$4.862,38");
    expect(partesVisuais?.querySelectorAll("span")).toHaveLength(2);
  });

  it("deve mostrar tempo relativo com a data absoluta no title", () => {
    renderSimples(
      <DateText
        valor="2026-10-21T14:32:00-03:00"
        formato="relativo"
        agora={new Date("2026-10-21T14:44:00-03:00")}
      />,
    );
    expect(screen.getByText("há 12 min")).toHaveAttribute("title", "21/10/2026 · 14:32");
  });
});

describe("KpiCard", () => {
  it("deve fazer de cada KPI um link e dar o valor completo ao valor abreviado", () => {
    renderComRotas(
      <KpiCard rotulo="Indicadores da carteira">
        <Kpi
          rotulo="Prêmio de outubro"
          valor={{ tipo: "moeda", valor: 286412.9 }}
          para="/apolices"
        />
        <Kpi
          rotulo="Cotações em aberto"
          valor={{ tipo: "inteiro", valor: 57 }}
          meta={<Atencao numero={12}>expiram em 48 h</Atencao>}
        />
        <Kpi
          rotulo="Apólices ativas"
          valor={{ tipo: "inteiro", valor: 1284 }}
          meta={<Delta valor={38}>novas no mês</Delta>}
        />
        <Kpi
          rotulo="Sinistralidade 12m"
          valor={{ tipo: "percentual", valor: 0.642 }}
          medidor={{ valor: 0.642, meta: 0.6 }}
        />
      </KpiCard>,
    );
    const link = screen.getByRole("link", { name: /Prêmio de outubro/ });
    expect(link).toHaveAttribute("href", "/apolices");
    expect(link).toHaveTextContent(`R$${ESP}286.412,90`);
    expect(screen.getByTitle(`R$${ESP}286.412,90`)).toBeInTheDocument();
    expect(screen.getByText("+38")).toBeInTheDocument();
    expect(screen.getByText(/acima da meta/)).toBeInTheDocument();
  });

  it("deve usar o rótulo curto no mobile e o longo a partir do md", () => {
    renderComRotas(
      <KpiCard rotulo="Indicadores">
        <Kpi
          rotulo="Conversão em 90 dias"
          rotuloCurto="Conversão 90 dias"
          valor={{ tipo: "percentual", valor: 0.316 }}
        />
      </KpiCard>,
    );
    expect(screen.getByText("Conversão 90 dias")).toHaveClass("md:hidden");
    expect(screen.getByText("Conversão em 90 dias")).toHaveClass("hidden", "md:block");
  });

  it("deve dar ao total do gráfico o formato de KPI (R$ na linha de base), não de herói", () => {
    renderSimples(<NumeroKpi valor={{ tipo: "moeda", valor: 47688.86 }} tamanho="total" />);
    const valor = screen.getByTitle(`R$${ESP}47.688,86`);
    expect(valor).toHaveClass("type-fig-total");
    expect(valor).toHaveTextContent("47,7");
    // Herói tem R$ sobrescrito (align); o formato de KPI não.
    expect(valor.innerHTML).not.toMatch(/align-/);
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderComRotas(
      <KpiCard rotulo="Indicadores">
        <Kpi rotulo="Apólices ativas" valor={{ tipo: "inteiro", valor: 10 }} para="/apolices" />
        <Kpi
          rotulo="Conversão"
          valor={{ tipo: "percentual", valor: 0.316 }}
          meta="6 de 19 cotações"
        />
      </KpiCard>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Breadcrumbs", () => {
  it("deve marcar a página atual e ter links nas anteriores", async () => {
    const { container } = renderComRotas(
      <Breadcrumbs
        itens={[
          { rotulo: "Apólices", href: "/apolices" },
          { rotulo: "AUTO-2026-000042", codigo: true },
        ]}
      />,
    );
    expect(screen.getByRole("navigation", { name: "Trilha de navegação" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Apólices" })).toHaveAttribute("href", "/apolices");
    expect(screen.getByText("AUTO-2026-000042")).toHaveAttribute("aria-current", "page");
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Pagination", () => {
  it("deve carregar mais pelo cursor e avisar o fim da lista", async () => {
    const user = userEvent.setup();
    const aoCarregarMais = vi.fn();
    const { rerender } = renderSimples(
      <Pagination
        exibidos={25}
        total={1284}
        temMais
        aoCarregarMais={aoCarregarMais}
        substantivo="apólices"
      />,
    );
    expect(screen.getByText("Mostrando 25 de 1.284 apólices")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Carregar mais" }));
    expect(aoCarregarMais).toHaveBeenCalledOnce();
    rerender(
      <Pagination exibidos={30} total={30} temMais={false} aoCarregarMais={aoCarregarMais} />,
    );
    expect(screen.getByText("Fim da lista")).toBeInTheDocument();
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <Pagination exibidos={25} total={1284} temMais aoCarregarMais={vi.fn()} carregando />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("EmptyState e ErrorState", () => {
  it("deve oferecer a ação do estado vazio", async () => {
    const { container } = renderSimples(
      <EmptyState
        icone={Inbox}
        titulo="Nenhuma cotação ainda"
        descricao="Comece pela primeira."
        acao={<button type="button">Nova cotação</button>}
      />,
    );
    expect(
      screen.getByRole("heading", { name: "Nenhuma cotação ainda", level: 2 }),
    ).toBeInTheDocument();
    expect(await axe(container)).toHaveNoViolations();
  });

  it("deve tentar de novo e copiar o ID de correlação", async () => {
    const user = userEvent.setup();
    const escrever = vi.spyOn(navigator.clipboard, "writeText");
    const aoTentar = vi.fn();
    const { container } = renderSimples(
      <ErrorState
        titulo="Não foi possível carregar as apólices"
        correlationId="7f3c9a2e-41b8"
        aoTentarDeNovo={aoTentar}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Tentar de novo" }));
    expect(aoTentar).toHaveBeenCalledOnce();
    await user.click(screen.getByRole("button", { name: "Copiar ID de correlação" }));
    expect(escrever).toHaveBeenCalledWith("7f3c9a2e-41b8");
    expect(await screen.findByRole("button", { name: "ID copiado" })).toBeInTheDocument();
    expect(await axe(container)).toHaveNoViolations();
  });
});

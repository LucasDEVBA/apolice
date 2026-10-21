import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { simularDesktop } from "@/test/media-query";
import { axe, renderSimples } from "@/test/utilitarios";

import { BarraDistribuicao } from "./BarraDistribuicao";
import { GraficoBarras } from "./GraficoBarras";

const DADOS = [
  { mes: "2026-08", valor: 5310 },
  { mes: "2026-09", valor: 0 },
  { mes: "2026-10", valor: 2860.5, projecao: 4150 },
];

describe("GraficoBarras", () => {
  it("deve alternar para a tabela acessível com caption e os mesmos dados", async () => {
    const user = userEvent.setup();
    renderSimples(
      <GraficoBarras
        dados={DADOS}
        rotulo="Prêmio emitido por mês"
        mesCorrente="2026-10"
        media={4000}
      />,
    );
    const alternar = screen.getByRole("button", { name: "Ver como tabela" });
    // Rótulo variável sem aria-pressed (as duas coisas juntas leriam "…gráfico, pressionado").
    expect(alternar).not.toHaveAttribute("aria-pressed");
    await user.click(alternar);
    const tabela = screen.getByRole("table", { name: "Prêmio emitido por mês" });
    const linhas = within(tabela).getAllByRole("row");
    expect(linhas).toHaveLength(4);
    const cabecalhosDeLinha = within(tabela).getAllByRole("rowheader");
    expect(cabecalhosDeLinha).toHaveLength(3);
    expect(cabecalhosDeLinha.at(-1)).toHaveTextContent("outubro de 2026 (parcial)");
    expect(within(tabela).getByText("R$ 4.150,00")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ver como gráfico" })).toBeInTheDocument();
  });

  it("deve pôr o valor da média na legenda no mobile (sem rótulo cobrindo barras)", () => {
    renderSimples(<GraficoBarras dados={DADOS} rotulo="Prêmio emitido por mês" media={4000} />);
    expect(screen.getByText("Média mensal · 4 mil")).toBeInTheDocument();
  });

  it("deve deixar só o nome da média na legenda no desktop (o valor vai na linha)", () => {
    simularDesktop();
    renderSimples(<GraficoBarras dados={DADOS} rotulo="Prêmio emitido por mês" media={4000} />);
    expect(screen.getByText("Média mensal")).toBeInTheDocument();
  });

  it("não deve ter violações de acessibilidade na versão em gráfico", async () => {
    const { container } = renderSimples(
      <GraficoBarras
        dados={DADOS}
        rotulo="Prêmio emitido por mês"
        mesCorrente="2026-10"
        media={4000}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it("não deve ter violações de acessibilidade na versão em tabela", async () => {
    const user = userEvent.setup();
    const { container } = renderSimples(
      <GraficoBarras dados={DADOS} rotulo="Prêmio emitido por mês" />,
    );
    await user.click(screen.getByRole("button", { name: "Ver como tabela" }));
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("BarraDistribuicao", () => {
  it("deve descrever a distribuição por extenso", async () => {
    const { container } = renderSimples(
      <BarraDistribuicao
        fatias={[
          { produto: "AUTO", valor: 58 },
          { produto: "RESID", valor: 17 },
          { produto: "VIDA", valor: 25 },
        ]}
      />,
    );
    expect(
      screen.getByRole("img", {
        name: "Distribuição por produto: Auto 58%, Residencial 17%, Vida 25%",
      }),
    ).toBeInTheDocument();
    expect(await axe(container)).toHaveNoViolations();
  });
});

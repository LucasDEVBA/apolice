import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import {
  ajustesDe,
  db,
  hoje,
  simularCancelamento,
  statusApolice,
  usuarioPorPersona,
} from "@/mocks/banco";
import { renderApp } from "@/test/app";
import { acharToast, textoMoeda } from "@/test/interacoes";
import { axe } from "@/test/utilitarios";

function apoliceAtivaDaAna(produto: "AUTO" | "RESID" | "VIDA") {
  const ana = usuarioPorPersona("corretora").id;
  const apolice = db().apolices.find(
    (a) =>
      a.corretorId === ana &&
      a.produtoCodigo === produto &&
      statusApolice(a) === "Ativa" &&
      a.endossos.length > 0,
  );
  return (
    apolice ??
    db().apolices.find(
      (a) => a.corretorId === ana && a.produtoCodigo === produto && statusApolice(a) === "Ativa",
    )!
  );
}

const SEM_LANDMARKS_DUPLICADOS = {
  rules: {
    "landmark-unique": { enabled: false },
    "landmark-no-duplicate-banner": { enabled: false },
  },
};

describe("Detalhe da apólice", () => {
  it("deve mostrar a face, as abas com contadores e não ter violações de acessibilidade", async () => {
    const apolice = apoliceAtivaDaAna("AUTO");
    renderApp(`/apolices/${apolice.id}`);
    expect(
      await screen.findByRole("heading", { level: 1, name: apolice.numero }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: `Coberturas, ${String(apolice.coberturas.length)}` }),
    ).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Endossos, 1" })).toBeInTheDocument();
    expect(screen.getByText(/Registro SRO/)).toHaveTextContent("simulado");
    expect(await axe(document.body, SEM_LANDMARKS_DUPLICADOS)).toHaveNoViolations();
  });

  it("deve simular a restituição pelo motivo escolhido e cancelar com confirmação", async () => {
    const user = userEvent.setup();
    const apolice = apoliceAtivaDaAna("AUTO");
    const esperado = simularCancelamento(
      apolice.premioLiquido + ajustesDe(apolice),
      apolice.vigenciaInicio,
      apolice.vigenciaFim,
      "PedidoDoSegurado",
      hoje(),
    );
    renderApp(`/apolices/${apolice.id}`);
    await screen.findByRole("heading", { level: 1, name: apolice.numero });
    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    const dialogo = await screen.findByRole("dialog", {
      name: `Cancelar a apólice ${apolice.numero}?`,
    });
    const confirmar = within(dialogo).getByRole("button", { name: "Cancelar apólice" });
    expect(confirmar).toHaveAttribute("aria-disabled", "true");

    await user.click(within(dialogo).getByRole("radio", { name: "Pedido do segurado" }));
    expect(await within(dialogo).findByText("Prazo curto")).toBeInTheDocument();
    expect(within(dialogo).getByText(textoMoeda(esperado.valorRestituicao))).toBeInTheDocument();
    expect(within(dialogo).getByText("O IOF não é restituído.")).toBeInTheDocument();

    await user.click(within(dialogo).getByRole("button", { name: "Cancelar apólice" }));
    expect(await acharToast(`Apólice ${apolice.numero} cancelada`)).toBeInTheDocument();
    expect(await screen.findByText(/Cancelada por pedido do segurado/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Endossar" })).not.toBeInTheDocument();
  });

  it("deve simular o endosso antes de efetivar e registrar o novo endosso", async () => {
    const user = userEvent.setup();
    const apolice = apoliceAtivaDaAna("AUTO");
    const endossosAntes = apolice.endossos.length;
    renderApp(`/apolices/${apolice.id}`);
    await screen.findByRole("heading", { level: 1, name: apolice.numero });
    await user.click(screen.getAllByRole("button", { name: "Endossar" })[0]!);
    const dialogo = await screen.findByRole("dialog", {
      name: `Endossar a apólice ${apolice.numero}`,
    });
    await user.click(within(dialogo).getByRole("radio", { name: "Inclusão de cobertura" }));
    const campo = within(dialogo).getByRole("combobox", { name: /Cobertura/ });
    campo.focus();
    await user.keyboard("{Enter}");
    await user.click(await screen.findByRole("option", { name: "Carro reserva (15 dias)" }));
    expect(await within(dialogo).findByText("A cobrar")).toBeInTheDocument();
    await user.click(within(dialogo).getByRole("button", { name: "Efetivar endosso" }));
    expect(
      await acharToast(`Endosso nº ${String(endossosAntes + 1)} registrado`),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("tab", { name: `Endossos, ${String(endossosAntes + 1)}` }),
    ).toBeInTheDocument();
  });

  it("deve abrir sinistro com validação da descrição e ir para o sinistro criado", async () => {
    const user = userEvent.setup();
    const apolice = apoliceAtivaDaAna("AUTO");
    renderApp(`/apolices/${apolice.id}`);
    await screen.findByRole("heading", { level: 1, name: apolice.numero });
    await user.click(screen.getAllByRole("button", { name: "Abrir sinistro" })[0]!);
    const dialogo = await screen.findByRole("dialog", {
      name: `Abrir sinistro na apólice ${apolice.numero}`,
    });
    const cobertura = within(dialogo).getByRole("combobox", { name: /Cobertura afetada/ });
    cobertura.focus();
    await user.keyboard("{Enter}");
    await user.click(await screen.findByRole("option", { name: "Vidros, faróis e retrovisores" }));
    const data = within(dialogo).getByLabelText(/Data da ocorrência/);
    await user.type(data, hoje());
    await user.type(within(dialogo).getByRole("textbox", { name: /Descrição/ }), "Trinca curta");
    await user.type(within(dialogo).getByRole("textbox", { name: /Valor reclamado/ }), "1800");
    await user.click(within(dialogo).getByRole("button", { name: "Abrir sinistro" }));
    expect(
      await within(dialogo).findByText("Descreva o ocorrido com pelo menos 20 caracteres."),
    ).toBeInTheDocument();

    await user.type(
      within(dialogo).getByRole("textbox", { name: /Descrição/ }),
      " no para-brisa causada por pedra na estrada.",
    );
    await user.click(within(dialogo).getByRole("button", { name: "Abrir sinistro" }));
    expect(
      await screen.findByRole("heading", { level: 1, name: /^Sinistro SIN-\d{4}-\d{6}$/ }),
    ).toBeInTheDocument();
    expect(await screen.findByText(/Triagem em andamento|Triado em/)).toBeInTheDocument();
  });
});

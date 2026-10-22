import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { db } from "@/mocks/banco";
import { renderApp } from "@/test/app";
import { acharToast } from "@/test/interacoes";

function sinistroAberto() {
  const sinistro = db().sinistros.find((s) => s.status === "Aberto");
  if (!sinistro) throw new Error("Seed sem sinistro aberto.");
  return sinistro;
}

describe("Detalhe do sinistro", () => {
  it("deve conduzir a regulação: iniciar análise, aprovar dentro do teto e registrar o pagamento", async () => {
    const user = userEvent.setup();
    const sinistro = sinistroAberto();
    const teto = Math.min(sinistro.valorReclamado, sinistro.lmiCobertura);
    renderApp(`/sinistros/${sinistro.id}`, { persona: "analista" });
    expect(
      await screen.findByRole("heading", { level: 1, name: `Sinistro ${sinistro.numero}` }),
    ).toBeInTheDocument();
    expect(screen.getByRole("list", { name: "Alertas da triagem" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Iniciar análise" }));
    await user.click(
      within(await screen.findByRole("dialog", { name: "Iniciar a análise?" })).getByRole(
        "button",
        { name: "Iniciar análise" },
      ),
    );
    expect(await acharToast("Análise iniciada")).toBeInTheDocument();

    await user.click(await screen.findByRole("button", { name: "Aprovar" }));
    const aprovar = await screen.findByRole("dialog", {
      name: `Aprovar o sinistro ${sinistro.numero}`,
    });
    const valor = within(aprovar).getByRole("textbox", { name: /Valor da indenização/ });
    await user.clear(valor);
    await user.type(valor, String(teto + 1000));
    await user.click(within(aprovar).getByRole("button", { name: "Aprovar indenização" }));
    expect(
      await within(aprovar).findByText(/Informe um valor maior que zero e de até/),
    ).toBeInTheDocument();

    await user.clear(valor);
    await user.type(valor, "50000");
    await user.type(
      within(aprovar).getByRole("textbox", { name: /Observação/ }),
      "Laudo médico anexado ao processo.",
    );
    await user.click(within(aprovar).getByRole("button", { name: "Aprovar indenização" }));
    expect(await acharToast(`Sinistro ${sinistro.numero} aprovado`)).toBeInTheDocument();

    await user.click(await screen.findByRole("button", { name: "Registrar pagamento" }));
    await user.click(
      within(await screen.findByRole("dialog", { name: "Registrar o pagamento?" })).getByRole(
        "button",
        { name: "Registrar pagamento" },
      ),
    );
    expect(await acharToast("Pagamento registrado")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Histórico" })).toBeInTheDocument();
    const historico = screen.getByRole("list", { name: "Histórico do sinistro" });
    expect(await within(historico).findByText("Pagamento registrado")).toBeInTheDocument();
    expect(db().sinistros.find((s) => s.id === sinistro.id)?.status).toBe("Pago");
  });

  it("deve negar com motivo de pelo menos 10 caracteres", async () => {
    const user = userEvent.setup();
    const emAnalise = db().sinistros.find((s) => s.status === "EmAnalise")!;
    renderApp(`/sinistros/${emAnalise.id}`, { persona: "admin" });
    await user.click(await screen.findByRole("button", { name: "Negar" }));
    const negar = await screen.findByRole("dialog", {
      name: `Negar o sinistro ${emAnalise.numero}`,
    });
    await user.type(within(negar).getByRole("textbox", { name: /Motivo/ }), "Curto");
    await user.click(within(negar).getByRole("button", { name: "Negar sinistro" }));
    expect(
      await within(negar).findByText("Explique o motivo com pelo menos 10 caracteres."),
    ).toBeInTheDocument();
    await user.type(
      within(negar).getByRole("textbox", { name: /Motivo/ }),
      " demais: dano anterior à vigência.",
    );
    await user.click(within(negar).getByRole("button", { name: "Negar sinistro" }));
    expect(await acharToast(`Sinistro ${emAnalise.numero} negado`)).toBeInTheDocument();
  });

  it("não deve oferecer ações de regulação ao corretor", async () => {
    const sinistro = sinistroAberto();
    renderApp(`/sinistros/${sinistro.id}`, { persona: "corretor" });
    expect(
      await screen.findByRole("heading", { level: 1, name: `Sinistro ${sinistro.numero}` }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Iniciar análise" })).not.toBeInTheDocument();
  });
});

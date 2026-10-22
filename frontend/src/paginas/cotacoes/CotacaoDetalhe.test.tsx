import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { db, statusCotacao, usuarioPorPersona } from "@/mocks/banco";
import { renderApp } from "@/test/app";
import { acharToast } from "@/test/interacoes";
import { hojeEmBrasilia } from "@/ui/utils/datas";

function cotacaoDaAna(status: "Calculada" | "Aceita") {
  const ana = usuarioPorPersona("corretora").id;
  const cotacao = db().cotacoes.find((c) => c.corretorId === ana && statusCotacao(c) === status);
  if (!cotacao) throw new Error(`Seed sem cotação ${status} da Ana.`);
  return cotacao;
}

describe("Detalhe da cotação", () => {
  it("deve aceitar a cotação com confirmação e liberar a emissão", async () => {
    const user = userEvent.setup();
    const cotacao = cotacaoDaAna("Calculada");
    renderApp(`/cotacoes/${cotacao.id}`);
    await user.click(await screen.findByRole("button", { name: "Aceitar cotação" }));
    const dialogo = await screen.findByRole("dialog", { name: "Aceitar cotação?" });
    // Foco inicial na ação segura, nunca na que muda o estado.
    expect(within(dialogo).getByRole("button", { name: "Voltar" })).toHaveFocus();
    await user.click(within(dialogo).getByRole("button", { name: "Aceitar cotação" }));
    expect(await acharToast("Cotação aceita")).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Emitir apólice" })).toBeInTheDocument();
  });

  it("deve emitir a apólice com Idempotency-Key e mostrar as boas-vindas quando o worker enviar", async () => {
    const user = userEvent.setup();
    const cotacao = cotacaoDaAna("Aceita");
    const { router } = renderApp(`/cotacoes/${cotacao.id}`);
    await user.click(await screen.findByRole("button", { name: "Emitir apólice" }));
    const dialogo = await screen.findByRole("dialog", { name: "Emitir apólice" });
    expect(within(dialogo).getByLabelText(/Início da vigência/)).toHaveValue(hojeEmBrasilia());
    await user.click(within(dialogo).getByRole("button", { name: "Emitir apólice" }));

    const numero = await screen.findByRole("heading", { level: 1, name: /^RESID-\d{4}-\d{6}$/ });
    expect(await acharToast(`Apólice ${numero.textContent} emitida`)).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/apolices\//));
    expect(db().idempotencia.size).toBe(1);

    await user.click(screen.getByRole("tab", { name: /Comunicações/ }));
    // Polling: a tela consulta de novo até a notificação do worker chegar (SPEC §5, outbox → fila).
    const boasVindas = await screen.findAllByText(
      `Bem-vindo(a)! Sua apólice ${numero.textContent} foi emitida`,
      {},
      { timeout: 15_000 },
    );
    expect(boasVindas.length).toBeGreaterThan(0);
    expect(screen.queryByText(/Enviando as boas-vindas/)).not.toBeInTheDocument();
  }, 40_000);
});

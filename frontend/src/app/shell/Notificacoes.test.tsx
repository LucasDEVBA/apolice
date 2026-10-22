import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { axe, renderSimples } from "@/test/utilitarios";

import { Notificacoes } from "./Notificacoes";

describe("Notificacoes", () => {
  it("deve dizer quantas não lidas há no nome acessível, com o ponto só quando houver", () => {
    const { rerender } = renderSimples(<Notificacoes naoLidas={3} />);
    expect(screen.getByRole("button", { name: "Notificações, 3 não lidas" })).toBeInTheDocument();
    rerender(<Notificacoes naoLidas={1} />);
    expect(screen.getByRole("button", { name: "Notificações, 1 não lida" })).toBeInTheDocument();
    rerender(<Notificacoes />);
    expect(screen.getByRole("button", { name: "Notificações" })).toBeInTheDocument();
  });

  it("deve abrir o painel sem inventar avisos e não ter violações", async () => {
    const user = userEvent.setup();
    renderSimples(<Notificacoes />);
    await user.click(screen.getByRole("button", { name: "Notificações" }));
    expect(await screen.findByRole("dialog", { name: "Notificações" })).toHaveTextContent(
      "Nenhuma notificação para você",
    );
    expect(await axe(document.body)).toHaveNoViolations();
  });
});

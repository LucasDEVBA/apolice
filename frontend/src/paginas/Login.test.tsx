import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { db, SENHA_MOCK } from "@/mocks/banco";
import { renderApp } from "@/test/app";
import { axe } from "@/test/utilitarios";

describe("Login", () => {
  it("deve entrar com a persona demo e ir ao dashboard com o escopo da carteira", async () => {
    const user = userEvent.setup();
    const { router } = renderApp("/login", { persona: null });
    await user.click(await screen.findByRole("button", { name: /Ana Ribeiro/ }));
    expect(
      await screen.findByRole("heading", { name: "Visão da carteira", level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/");
    expect(await screen.findByText("Escopo: sua carteira")).toBeInTheDocument();
  });

  it("deve mandar quem não está logado para o login e voltar à rota pedida depois", async () => {
    const user = userEvent.setup();
    const { router } = renderApp("/sinistros", { persona: null });
    expect(await screen.findByRole("heading", { name: "Entrar", level: 1 })).toBeInTheDocument();
    await user.click(await screen.findByRole("button", { name: /Carla Souza/ }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/sinistros"));
    expect(await screen.findByRole("heading", { name: "Sinistros", level: 1 })).toBeInTheDocument();
  });

  it("deve entrar com e-mail e senha e mostrar o erro da API sem revelar qual campo falhou", async () => {
    const user = userEvent.setup();
    renderApp("/login", { persona: null });
    await user.type(await screen.findByRole("textbox", { name: "E-mail" }), "admin@apolice.dev");
    await user.type(screen.getByLabelText("Senha"), "senha-errada");
    await user.click(screen.getByRole("button", { name: "Entrar" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("E-mail ou senha inválidos.");

    await user.clear(screen.getByLabelText("Senha"));
    await user.type(screen.getByLabelText("Senha"), SENHA_MOCK);
    await user.click(screen.getByRole("button", { name: "Entrar" }));
    expect(await screen.findByText("Escopo global · todas as carteiras")).toBeInTheDocument();
  });

  it("deve esconder as personas quando o login demo estiver desligado", async () => {
    db().features.loginDemo = false;
    renderApp("/login", { persona: null });
    expect(await screen.findByText("Acesse com o seu e-mail e senha.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Ana Ribeiro/ })).not.toBeInTheDocument();
  });

  it("não deve dizer que os dados são recriados diariamente nem ter violações de acessibilidade", async () => {
    const { container } = renderApp("/login", { persona: null });
    await screen.findByRole("button", { name: /Ana Ribeiro/ });
    expect(container).not.toHaveTextContent(/recriados diariamente/);
    expect(await axe(document.body, { rules: { region: { enabled: true } } })).toHaveNoViolations();
  });
});

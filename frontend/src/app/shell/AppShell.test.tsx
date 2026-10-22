import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { renderApp } from "@/test/app";
import { axe } from "@/test/utilitarios";

/**
 * Sem CSS no jsdom, sidebar, rail e top bar mobile aparecem todos ao mesmo tempo; cada um tem um
 * contêiner com data-shell, e a consulta é feita dentro dele (nada de índice de landmark).
 */
function regiaoDoShell(nome: "sidebar" | "rail"): HTMLElement {
  const regiao = document.querySelector<HTMLElement>(`[data-shell="${nome}"]`);
  if (!regiao) throw new Error(`Contêiner do shell "${nome}" não encontrado.`);
  return regiao;
}

describe("AppShell", () => {
  it("deve ter o skip link como primeiro foco na carga do documento", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await user.tab();
    const pular = screen.getByRole("link", { name: "Pular para o conteúdo" });
    expect(pular).toHaveFocus();
    expect(pular).toHaveAttribute("href", "#conteudo");
    expect(document.getElementById("conteudo")?.tagName).toBe("MAIN");
  });

  it("deve marcar a rota ativa e restringir o Admin para quem não é Admin", async () => {
    renderApp("/cotacoes");
    await screen.findByRole("heading", { name: "Cotações", level: 1 });
    const nav = within(regiaoDoShell("sidebar")).getByRole("navigation", { name: "Principal" });
    expect(within(nav).getByRole("link", { name: /^Cotações(, \d+)?$/ })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(nav).queryByRole("link", { name: "Admin" })).not.toBeInTheDocument();
    expect(within(nav).getByText("(restrito ao perfil Admin)")).toBeInTheDocument();
    expect(within(nav).getByRole("group", { name: "Filas salvas" })).toBeInTheDocument();
  });

  it("deve bloquear a página do Admin para quem não é Admin (403 amigável)", async () => {
    renderApp("/admin/tarifas", { persona: "analista" });
    expect(
      await screen.findByRole("heading", { name: "Acesso restrito", level: 1 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Disponível para o perfil Admin" }),
    ).toBeInTheDocument();
  });

  it("deve abrir o Admin para a persona Admin, com a trilha da seção", async () => {
    renderApp("/admin/tarifas", { persona: "admin" });
    expect(await screen.findByRole("heading", { name: "Tarifas", level: 1 })).toBeInTheDocument();
    const trilha = screen.getByRole("navigation", { name: "Trilha de navegação" });
    expect(within(trilha).getByRole("link", { name: "Admin" })).toHaveAttribute(
      "href",
      "/admin/tarifas",
    );
    expect(within(trilha).getByText("Tarifas")).toHaveAttribute("aria-current", "page");
  });

  it("deve mover o foco para o H1 ao navegar e atualizar o título do documento", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await user.click(within(regiaoDoShell("sidebar")).getByRole("link", { name: "Apólices" }));
    const titulo = await screen.findByRole("heading", { name: "Apólices", level: 1 });
    await waitFor(() => expect(titulo).toHaveFocus());
    expect(document.title).toBe("Apólices · Apólice");
  });

  it("deve anunciar a primeira tela depois do login (foco no H1 vindo de /login)", async () => {
    const user = userEvent.setup();
    renderApp("/login", { persona: null });
    await screen.findByRole("heading", { name: "Entrar", level: 1 });
    expect(document.title).toBe("Entrar · Apólice");
    await user.click(await screen.findByRole("button", { name: /Ana Ribeiro/ }));
    const titulo = await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await waitFor(() => expect(titulo).toHaveFocus());
  });

  it("deve abrir a paleta com Ctrl K, navegar pelo teclado e anunciar os resultados", async () => {
    const user = userEvent.setup();
    const { router } = renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await user.keyboard("{Control>}k{/Control}");
    const busca = await screen.findByRole("combobox", { name: "Buscar comando ou página" });
    expect(busca).toHaveFocus();
    await user.keyboard("sinis");
    // A página Sinistros e as três buscas de registros pelo termo.
    expect(screen.getAllByRole("option")).toHaveLength(4);
    expect(screen.getAllByRole("option")[0]).toHaveTextContent("Sinistros");
    await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("4 resultados."));
    await user.keyboard("{Enter}");
    await waitFor(() => expect(router.state.location.pathname).toBe("/sinistros"));
  });

  it("deve devolver o foco a quem estava focado ao fechar a paleta com Esc", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    const gatilho = within(regiaoDoShell("rail")).getByRole("button", {
      name: "Abrir menu completo",
    });
    gatilho.focus();
    await user.keyboard("{Control>}k{/Control}");
    await screen.findByRole("combobox", { name: "Buscar comando ou página" });
    await user.keyboard("{Escape}");
    await waitFor(() => expect(gatilho).toHaveFocus());
  });

  it("deve levar o termo da paleta para a busca da lista de apólices", async () => {
    const user = userEvent.setup();
    const { router } = renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await user.keyboard("{Control>}k{/Control}");
    await user.keyboard("Rafael");
    expect(screen.getAllByRole("option")[0]).toHaveTextContent("Buscar “Rafael” em apólices");
    await user.keyboard("{Enter}");
    await waitFor(() => expect(router.state.location.search).toBe("?busca=Rafael"));
    expect((await screen.findAllByText("Rafael Nogueira Castro")).length).toBeGreaterThan(0);
    expect(screen.queryByText("Patrícia Menezes")).not.toBeInTheDocument();
  });

  it("deve devolver o foco ao ☰ ao fechar o drawer com Esc e ir ao H1 ao navegar por ele", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    const abrir = screen.getByRole("button", { name: "Abrir menu" });
    await user.click(abrir);
    const drawer = await screen.findByRole("dialog", { name: "Menu" });
    // Foco inicial no item ativo, não no seletor de corretora.
    await waitFor(() =>
      expect(within(drawer).getByRole("link", { name: "Dashboard" })).toHaveFocus(),
    );
    await user.keyboard("{Escape}");
    await waitFor(() => expect(abrir).toHaveFocus());

    await user.click(abrir);
    const reaberto = await screen.findByRole("dialog", { name: "Menu" });
    await user.click(within(reaberto).getByRole("link", { name: /^Cotações(, \d+)?$/ }));
    const titulo = await screen.findByRole("heading", { name: "Cotações", level: 1 });
    await waitFor(() => expect(titulo).toHaveFocus());
  });

  it("deve ter o sino de notificações na topbar e na top bar mobile", async () => {
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    expect(screen.getAllByRole("button", { name: "Notificações" })).toHaveLength(2);
  });

  it("deve mostrar a página 404 dentro do shell", async () => {
    renderApp("/rota-que-nao-existe");
    expect(
      await screen.findByRole("heading", { name: "Página não encontrada", level: 1 }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Voltar ao Dashboard" })).toHaveAttribute("href", "/");
  });

  it("não deve ter violações de acessibilidade no shell", async () => {
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    // Sem CSS no jsdom, as variantes responsivas (rail, sidebar, top bar mobile) aparecem todas ao
    // mesmo tempo; no navegador só uma é exibida. Unicidade de landmarks fica para o E2E com axe.
    const resultado = await axe(document.body, {
      rules: {
        region: { enabled: true },
        "landmark-unique": { enabled: false },
        "landmark-no-duplicate-banner": { enabled: false },
      },
    });
    expect(resultado).toHaveNoViolations();
  });
});

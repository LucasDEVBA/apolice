import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { LucideProvider } from "lucide-react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";

import { axe } from "@/test/utilitarios";
import { TooltipProvider } from "@/ui/componentes/Tooltip";

import { PersonaProvider } from "../PersonaContexto";
import type { IdPersona } from "../personas";
import { rotas } from "../rotas";

function renderApp(rota: string, persona: IdPersona = "corretora") {
  const router = createMemoryRouter(rotas, { initialEntries: [rota] });
  render(
    <LucideProvider>
      <TooltipProvider>
        <PersonaProvider inicial={persona}>
          <RouterProvider router={router} />
        </PersonaProvider>
      </TooltipProvider>
    </LucideProvider>,
  );
  return router;
}

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
    expect(within(nav).getByRole("link", { name: "Cotações" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(nav).queryByRole("link", { name: "Admin" })).not.toBeInTheDocument();
    expect(within(nav).getByText("(restrito ao perfil Admin)")).toBeInTheDocument();
    expect(within(nav).getByRole("group", { name: "Filas salvas" })).toBeInTheDocument();
  });

  it("deve mostrar o Admin para a persona Admin e bloquear a página para as demais", async () => {
    renderApp("/admin/tarifas", "analista");
    expect(
      await screen.findByRole("heading", { name: "Disponível para o perfil Admin" }),
    ).toBeInTheDocument();
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
    renderApp("/login");
    await screen.findByRole("heading", { name: "Entrar", level: 1 });
    expect(document.title).toBe("Entrar · Apólice");
    await user.click(screen.getByRole("link", { name: "Explorar o layout" }));
    const titulo = await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await waitFor(() => expect(titulo).toHaveFocus());
  });

  it("deve abrir a paleta com Ctrl K, navegar pelo teclado e anunciar os resultados", async () => {
    const user = userEvent.setup();
    const router = renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await user.keyboard("{Control>}k{/Control}");
    const busca = await screen.findByRole("combobox", { name: "Buscar comando ou página" });
    expect(busca).toHaveFocus();
    await user.keyboard("sinis");
    expect(screen.getAllByRole("option")).toHaveLength(1);
    await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("1 resultado."));
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

  it("deve esconder a lista vazia da paleta e dizer que nada foi encontrado", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByRole("heading", { name: "Visão da carteira", level: 1 });
    await user.keyboard("{Control>}k{/Control}");
    await user.keyboard("zzz");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(screen.getByRole("combobox")).toHaveAttribute("aria-expanded", "false");
    await waitFor(() =>
      expect(screen.getByRole("status")).toHaveTextContent("Nenhum comando para “zzz”."),
    );
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
    await user.click(within(reaberto).getByRole("link", { name: "Cotações" }));
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

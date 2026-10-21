import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";

import { axe, renderSimples } from "@/test/utilitarios";

import { Button } from "./Button";
import { ConfirmDialog, Dialog, Drawer } from "./Dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "./DropdownMenu";
import { notificar } from "./notificar";
import { PreferenciasAparencia, ThemeToggle } from "./ThemeToggle";
import { Toaster } from "./Toaster";
import { Tooltip } from "./Tooltip";

describe("Dialog", () => {
  it("deve prender o foco, fechar com Esc e devolver o foco ao gatilho", async () => {
    const user = userEvent.setup();
    renderSimples(
      <Dialog
        gatilho={<Button>Endossar</Button>}
        titulo="Novo endosso"
        descricao="Simule antes de confirmar."
      >
        <label htmlFor="lmi">Novo LMI</label>
        <input id="lmi" />
      </Dialog>,
    );
    const gatilho = screen.getByRole("button", { name: "Endossar" });
    await user.click(gatilho);
    const dialogo = await screen.findByRole("dialog", { name: "Novo endosso" });
    expect(dialogo).toHaveAccessibleDescription("Simule antes de confirmar.");
    expect(screen.getByRole("textbox", { name: "Novo LMI" })).toHaveFocus();
    // Tab circula dentro do diálogo (campo → Fechar → campo): o foco não escapa para a página.
    await user.tab();
    expect(screen.getByRole("button", { name: "Fechar" })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole("textbox", { name: "Novo LMI" })).toHaveFocus();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(gatilho).toHaveFocus();
  });

  it("não deve ter violações de acessibilidade aberto", async () => {
    renderSimples(
      <Dialog
        aberto
        titulo="Cancelar apólice"
        descricao="O IOF não é restituído."
        rodape={<Button>Confirmar</Button>}
      >
        <p>Conteúdo</p>
      </Dialog>,
    );
    await screen.findByRole("dialog");
    expect(await axe(document.body)).toHaveNoViolations();
  });
});

describe("ConfirmDialog", () => {
  it("deve focar a ação segura e só fechar depois de confirmar", async () => {
    const user = userEvent.setup();
    let concluir: () => void = () => undefined;
    const aoConfirmar = vi.fn(() => new Promise<void>((ok) => (concluir = ok)));
    renderSimples(
      <ConfirmDialog
        gatilho={<Button variante="perigo-contorno">Cancelar apólice</Button>}
        titulo="Cancelar AUTO-2026-000042"
        rotuloConfirmar="Cancelar apólice"
        rotuloConfirmando="Cancelando…"
        aoConfirmar={aoConfirmar}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Cancelar apólice" }));
    await screen.findByRole("dialog");
    expect(screen.getByRole("button", { name: "Voltar" })).toHaveFocus();
    const confirmar = screen.getAllByRole("button", { name: "Cancelar apólice" }).at(-1);
    await user.click(confirmar!);
    expect(aoConfirmar).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Cancelando…" })).toHaveAttribute(
      "aria-busy",
      "true",
    );
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    await act(async () => {
      concluir();
      await Promise.resolve();
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("deve manter a confirmação bloqueada até a condição ser atendida", async () => {
    const user = userEvent.setup();
    const aoConfirmar = vi.fn();
    renderSimples(
      <ConfirmDialog
        aberto
        titulo="Cancelar"
        rotuloConfirmar="Cancelar apólice"
        confirmarDesabilitado
        aoConfirmar={aoConfirmar}
      />,
    );
    await user.click(await screen.findByRole("button", { name: "Cancelar apólice" }));
    expect(aoConfirmar).not.toHaveBeenCalled();
  });
});

describe("Dialog controlado (sem gatilho)", () => {
  it("deve devolver o foco a quem o abriu, como a paleta aberta por atalho", async () => {
    const user = userEvent.setup();
    function Controlado() {
      const [aberto, setAberto] = useState(false);
      return (
        <>
          <button type="button" onClick={() => setAberto(true)}>
            Buscar
          </button>
          <Dialog aberto={aberto} aoMudar={setAberto} titulo="Buscar e navegar">
            <label htmlFor="termo">Termo</label>
            <input id="termo" />
          </Dialog>
        </>
      );
    }
    renderSimples(<Controlado />);
    const buscar = screen.getByRole("button", { name: "Buscar" });
    await user.click(buscar);
    expect(await screen.findByRole("textbox", { name: "Termo" })).toHaveFocus();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    await waitFor(() => expect(buscar).toHaveFocus());
  });
});

describe("Drawer", () => {
  function MenuDeTeste() {
    const [aberto, setAberto] = useState(false);
    return (
      <>
        <button type="button" onClick={() => setAberto(true)}>
          Abrir menu
        </button>
        <Drawer aberto={aberto} aoMudar={setAberto} titulo="Menu">
          <nav aria-label="Principal">
            <a href="/">Dashboard</a>
            <a href="/cotacoes" aria-current="page">
              Cotações
            </a>
          </nav>
        </Drawer>
      </>
    );
  }

  it("deve ter título acessível e fechar com Esc", async () => {
    const user = userEvent.setup();
    const aoMudar = vi.fn();
    renderSimples(
      <Drawer aberto aoMudar={aoMudar} titulo="Menu">
        <a href="/cotacoes">Cotações</a>
      </Drawer>,
    );
    expect(await screen.findByRole("dialog", { name: "Menu" })).toBeInTheDocument();
    await user.keyboard("{Escape}");
    expect(aoMudar).toHaveBeenCalledWith(false);
  });

  it("deve focar o item ativo ao abrir e devolver o foco ao ☰ ao fechar com Esc", async () => {
    const user = userEvent.setup();
    renderSimples(<MenuDeTeste />);
    const abrir = screen.getByRole("button", { name: "Abrir menu" });
    await user.click(abrir);
    await waitFor(() => expect(screen.getByRole("link", { name: "Cotações" })).toHaveFocus());
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    await waitFor(() => expect(abrir).toHaveFocus());
  });

  it("deve fechar pelo botão Fechar menu (saída sem Esc para leitor de tela no celular)", async () => {
    const user = userEvent.setup();
    renderSimples(<MenuDeTeste />);
    await user.click(screen.getByRole("button", { name: "Abrir menu" }));
    await user.click(await screen.findByRole("button", { name: "Fechar menu" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Abrir menu" })).toHaveFocus();
  });

  it("não deve ter violações de acessibilidade aberto", async () => {
    renderSimples(
      <Drawer aberto aoMudar={vi.fn()} titulo="Menu">
        <nav aria-label="Principal">
          <a href="/cotacoes">Cotações</a>
        </nav>
      </Drawer>,
    );
    await screen.findByRole("dialog", { name: "Menu" });
    expect(await axe(document.body)).toHaveNoViolations();
  });
});

describe("DropdownMenu", () => {
  it("deve abrir pelo teclado, navegar com setas e escolher com Enter", async () => {
    const user = userEvent.setup();
    const aoEndossar = vi.fn();
    const aoCopiar = vi.fn();
    renderSimples(
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variante="secundario">Ações</Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem onSelect={aoEndossar}>Endossar</DropdownMenuItem>
          <DropdownMenuItem onSelect={aoCopiar}>Copiar número</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>,
    );
    screen.getByRole("button", { name: "Ações" }).focus();
    await user.keyboard("{Enter}");
    const menu = await screen.findByRole("menu");
    expect(menu).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("menuitem", { name: "Endossar" })).toHaveFocus());
    expect(await axe(document.body)).toHaveNoViolations();
    await user.keyboard("{ArrowDown}{Enter}");
    expect(aoCopiar).toHaveBeenCalledOnce();
    expect(aoEndossar).not.toHaveBeenCalled();
  });
});

describe("ThemeToggle", () => {
  it("deve aplicar e guardar o tema escolhido", async () => {
    const user = userEvent.setup();
    renderSimples(<ThemeToggle comDensidade />);
    screen.getByRole("button", { name: /Aparência/ }).focus();
    await user.keyboard("{Enter}");
    await user.click(await screen.findByRole("menuitemradio", { name: "Escuro" }));
    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
    expect(window.localStorage.getItem("apolice.tema")).toBe("escuro");
    expect(screen.getByRole("button", { name: "Aparência: tema escuro" })).toBeInTheDocument();
  });

  it("não deve ter violações de acessibilidade com o menu aberto", async () => {
    const user = userEvent.setup();
    renderSimples(<ThemeToggle comDensidade />);
    await user.click(screen.getByRole("button", { name: /Aparência/ }));
    await screen.findByRole("menu");
    expect(await axe(document.body)).toHaveNoViolations();
  });

  it("deve aplicar a densidade compacta", async () => {
    const user = userEvent.setup();
    renderSimples(<ThemeToggle comDensidade />);
    screen.getByRole("button", { name: /Aparência/ }).focus();
    await user.keyboard("{Enter}");
    await user.click(await screen.findByRole("menuitemradio", { name: "Compacta" }));
    expect(document.documentElement).toHaveAttribute("data-density", "compact");
  });
});

describe("PreferenciasAparencia", () => {
  it("deve trocar o tema pelos segmentos e não ter violações", async () => {
    const user = userEvent.setup();
    const { container } = renderSimples(
      <div className="on-navy">
        <PreferenciasAparencia />
      </div>,
    );
    await user.click(screen.getByRole("radio", { name: "Escuro" }));
    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Tooltip", () => {
  it("deve aparecer no foco e não ter violações", async () => {
    const user = userEvent.setup();
    renderSimples(
      <Tooltip conteudo="Restrito ao perfil Admin">
        <button type="button">Tarifas</button>
      </Tooltip>,
    );
    await user.tab();
    expect(await screen.findByRole("tooltip")).toHaveTextContent("Restrito ao perfil Admin");
    expect(await axe(document.body)).toHaveNoViolations();
  });
});

describe("Toaster + notificar", () => {
  it("deve mostrar o toast com ação e fechar pelo botão", async () => {
    const user = userEvent.setup();
    const aoVer = vi.fn();
    renderSimples(<Toaster />);
    act(() => {
      notificar.sucesso({
        titulo: "Apólice emitida",
        descricao: "AUTO-2026-000051",
        acao: { rotulo: "Ver apólice", aoClicar: aoVer },
      });
    });
    expect(await screen.findByText("Apólice emitida")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Ver apólice" }));
    expect(aoVer).toHaveBeenCalledOnce();
    await waitFor(() => expect(screen.queryByText("Apólice emitida")).not.toBeInTheDocument());
  });

  it("deve anunciar o erro na região assertive", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderSimples(<Toaster />);
    act(() => {
      notificar.erro({
        titulo: "Não foi possível emitir",
        descricao: "A cotação expirou.",
        correlationId: "7f3c-41b8",
      });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(document.querySelector('[data-anunciador="assertive"]')).toHaveTextContent(
      "Não foi possível emitir. A cotação expirou.",
    );
    expect(await screen.findByRole("button", { name: /Copiar ID/ })).toBeInTheDocument();
    vi.useRealTimers();
  });

  it("deve copiar o ID com retorno visível e sem quebrar sem área de transferência", async () => {
    const user = userEvent.setup();
    renderSimples(<Toaster />);
    act(() => {
      notificar.erro({ titulo: "Falhou", correlationId: "7f3c-41b8" });
    });
    await user.click(await screen.findByRole("button", { name: /Copiar ID/ }));
    expect(await screen.findByRole("button", { name: /ID copiado/ })).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe("7f3c-41b8");
  });

  it("não deve ter violações de acessibilidade com toasts abertos", async () => {
    renderSimples(<Toaster />);
    act(() => {
      notificar.sucesso({
        titulo: "Apólice emitida",
        acao: { rotulo: "Ver apólice", aoClicar: vi.fn() },
      });
      notificar.erro({ titulo: "Não foi possível emitir", correlationId: "7f3c-41b8" });
    });
    await screen.findByText("Apólice emitida");
    expect(await axe(document.body)).toHaveNoViolations();
  });
});

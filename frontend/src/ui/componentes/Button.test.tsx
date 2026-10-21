import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ArrowRight, Plus } from "lucide-react";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";

import { axe, renderComRotas, renderSimples } from "@/test/utilitarios";

import { Button } from "./Button";
import { IconButton } from "./IconButton";

describe("Button", () => {
  it("deve disparar o clique pelo mouse, pelo Enter e pelo Espaço", async () => {
    const user = userEvent.setup();
    const aoClicar = vi.fn();
    renderSimples(
      <Button icone={Plus} onClick={aoClicar}>
        Nova cotação
      </Button>,
    );
    const botao = screen.getByRole("button", { name: "Nova cotação" });
    await user.click(botao);
    await user.keyboard("{Enter}");
    await user.keyboard(" ");
    expect(aoClicar).toHaveBeenCalledTimes(3);
  });

  it("deve bloquear o clique e anunciar aria-busy durante o carregamento", async () => {
    const user = userEvent.setup();
    const aoClicar = vi.fn();
    renderSimples(
      <Button carregando rotuloCarregando="Emitindo…" onClick={aoClicar}>
        Emitir apólice
      </Button>,
    );
    const botao = screen.getByRole("button", { name: "Emitindo…" });
    expect(botao).toHaveAttribute("aria-busy", "true");
    expect(botao).toHaveAttribute("aria-disabled", "true");
    await user.click(botao);
    expect(aoClicar).not.toHaveBeenCalled();
  });

  it("deve segurar o submit implícito do formulário quando desabilitado", async () => {
    const user = userEvent.setup();
    const aoEnviar = vi.fn((e: SubmitEvent) => e.preventDefault());
    renderSimples(
      <form onSubmit={(e) => aoEnviar(e.nativeEvent)}>
        <Button type="submit" desabilitado>
          Revisar cotação
        </Button>
      </form>,
    );
    const botao = screen.getByRole("button", { name: "Revisar cotação" });
    expect(botao).toHaveAttribute("aria-disabled", "true");
    // aria-disabled mantém o botão focável (o leitor de tela o encontra), mas sem efeito.
    botao.focus();
    expect(botao).toHaveFocus();
    await user.click(botao);
    expect(aoEnviar).not.toHaveBeenCalled();
  });

  it("deve aplicar o visual a um link com asChild", () => {
    renderComRotas(
      <Button asChild>
        <a href="/cotacoes/nova">Nova cotação</a>
      </Button>,
    );
    expect(screen.getByRole("link", { name: "Nova cotação" })).toHaveClass("bg-primary");
  });

  it("deve repassar onClick, ref e ícones com asChild", async () => {
    const user = userEvent.setup();
    const aoClicar = vi.fn((e: { preventDefault: () => void }) => e.preventDefault());
    let elemento: HTMLButtonElement | null = null;
    renderComRotas(
      <Button
        asChild
        icone={Plus}
        iconeDireita={ArrowRight}
        onClick={aoClicar}
        ref={(el) => {
          elemento = el;
        }}
      >
        <a href="/cotacoes/nova">Nova cotação</a>
      </Button>,
    );
    const link = screen.getByRole("link", { name: "Nova cotação" });
    await user.click(link);
    expect(aoClicar).toHaveBeenCalledOnce();
    expect(elemento).toBe(link);
    // Os dois ícones entram DENTRO do link, em volta do texto.
    expect(link.querySelectorAll("svg")).toHaveLength(2);
  });

  it("deve bloquear o clique do link com asChild quando desabilitado", async () => {
    const user = userEvent.setup();
    const aoClicar = vi.fn();
    renderComRotas(
      <Button asChild desabilitado onClick={aoClicar}>
        <a href="/cotacoes/nova">Nova cotação</a>
      </Button>,
    );
    const link = screen.getByRole("link", { name: "Nova cotação" });
    expect(link).toHaveAttribute("aria-disabled", "true");
    await user.click(link);
    expect(aoClicar).not.toHaveBeenCalled();
  });

  it("deve travar a largura medida ANTES do rótulo no gerúndio", async () => {
    const user = userEvent.setup();
    // O jsdom não tem layout: a largura vem do texto renderizado (120 px em repouso, 80 depois).
    const medir = vi
      .spyOn(HTMLElement.prototype, "getBoundingClientRect")
      .mockImplementation(function (this: HTMLElement) {
        const largura = this.textContent.includes("Emitindo") ? 80 : 120;
        return { width: largura } as DOMRect;
      });
    function Alternavel() {
      const [carregando, setCarregando] = useState(false);
      return (
        <>
          <Button carregando={carregando} rotuloCarregando="Emitindo…">
            Emitir apólice
          </Button>
          <button type="button" onClick={() => setCarregando((v) => !v)}>
            alternar
          </button>
        </>
      );
    }
    renderSimples(<Alternavel />);
    await user.click(screen.getByRole("button", { name: "alternar" }));
    expect(screen.getByRole("button", { name: "Emitindo…" }).style.width).toBe("120px");
    await user.click(screen.getByRole("button", { name: "alternar" }));
    expect(screen.getByRole("button", { name: "Emitir apólice" }).style.width).toBe("");
    medir.mockRestore();
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <>
        <Button>Primário</Button>
        <Button variante="secundario">Secundário</Button>
        <Button variante="perigo">Cancelar apólice</Button>
        <Button carregando rotuloCarregando="Salvando…">
          Salvar
        </Button>
      </>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("IconButton", () => {
  it("deve expor o rótulo como nome acessível e mostrar tooltip no foco", async () => {
    const user = userEvent.setup();
    renderSimples(<IconButton icone={Plus} rotulo="Adicionar cobertura" comTooltip />);
    await user.tab();
    expect(screen.getByRole("button", { name: "Adicionar cobertura" })).toHaveFocus();
    expect(await screen.findByRole("tooltip")).toHaveTextContent("Adicionar cobertura");
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(<IconButton icone={Plus} rotulo="Adicionar" indicador />);
    expect(await axe(container)).toHaveNoViolations();
  });
});

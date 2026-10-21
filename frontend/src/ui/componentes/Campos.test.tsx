import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";

import { axe, renderSimples } from "@/test/utilitarios";

import { GrupoCartoesEscolha } from "./CartaoEscolha";
import { Checkbox } from "./Checkbox";
import { CampoBusca, ChipFiltro } from "./FiltrosTabela";
import { FormField } from "./FormField";
import { Input } from "./Input";
import { InputMoeda } from "./InputMoeda";
import { SegmentedControl } from "./SegmentedControl";
import { Select } from "./Select";
import { SeloObrigatoria, Switch } from "./Switch";
import { ContadorCaracteres, Textarea } from "./Textarea";

describe("FormField + Input", () => {
  it("deve ligar rótulo, ajuda e erro ao campo por id e aria-describedby", () => {
    renderSimples(
      <FormField
        rotulo="LMI"
        ajuda="Entre R$ 50.000,00 e R$ 500.000,00"
        erro="Informe um LMI dentro da faixa."
        obrigatorio
      >
        {(campo) => <Input {...campo} prefixo="LMI R$" />}
      </FormField>,
    );
    const campo = screen.getByRole("textbox", { name: /LMI/ });
    expect(campo).toHaveAttribute("aria-invalid", "true");
    expect(campo).toHaveAttribute("aria-required", "true");
    expect(campo).toHaveAccessibleDescription(
      "Entre R$ 50.000,00 e R$ 500.000,00 Informe um LMI dentro da faixa.",
    );
  });

  it("deve focar o campo ao clicar no rótulo e aceitar digitação", async () => {
    const user = userEvent.setup();
    renderSimples(
      <FormField rotulo="Nome do segurado">{(campo) => <Input {...campo} />}</FormField>,
    );
    await user.click(screen.getByText("Nome do segurado"));
    const campo = screen.getByRole("textbox", { name: "Nome do segurado" });
    expect(campo).toHaveFocus();
    await user.keyboard("Lucas Andrade");
    expect(campo).toHaveValue("Lucas Andrade");
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <FormField
        rotulo="E-mail"
        ajuda="Usado para a apólice em PDF"
        erro="Informe um e-mail válido."
      >
        {(campo) => <Input {...campo} type="email" />}
      </FormField>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Textarea + ContadorCaracteres", () => {
  function Controlado({ inicial = "" }: { inicial?: string }) {
    const [valor, setValor] = useState(inicial);
    return (
      <FormField
        rotulo="Descrição"
        ajuda="Entre 20 e 2.000 caracteres"
        contador={<ContadorCaracteres atual={valor.length} maximo={2000} />}
      >
        {(campo) => (
          <Textarea
            {...campo}
            value={valor}
            onChange={(e) => setValor(e.target.value)}
            maxLength={2000}
          />
        )}
      </FormField>
    );
  }

  it("deve mostrar a contagem e informar o limite na descrição do campo", async () => {
    const user = userEvent.setup();
    renderSimples(<Controlado />);
    const campo = screen.getByRole("textbox", { name: "Descrição" });
    await user.type(campo, "Colisão");
    expect(screen.getByText("7 de 2.000")).toBeInTheDocument();
    expect(campo).toHaveAccessibleDescription(
      "Entre 20 e 2.000 caracteres Máximo de 2.000 caracteres.",
    );
  });

  it("deve anunciar quantos restam só perto do limite", () => {
    renderSimples(
      <>
        <ContadorCaracteres atual={1990} maximo={2000} />
        <ContadorCaracteres atual={10} maximo={2000} />
      </>,
    );
    expect(screen.getByText("Restam 10 caracteres.")).toBeInTheDocument();
    expect(screen.queryByText(/Restam 1\.990/)).not.toBeInTheDocument();
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(<Controlado inicial="Colisão parcial" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("InputMoeda", () => {
  function Controlado({ aoMudar }: { aoMudar: (v: number | undefined) => void }) {
    const [valor, setValor] = useState<number | undefined>(undefined);
    return (
      <FormField rotulo="LMI">
        {(campo) => (
          <InputMoeda
            {...campo}
            prefixo="LMI R$"
            valor={valor}
            aoMudar={(v) => {
              setValor(v);
              aoMudar(v);
            }}
          />
        )}
      </FormField>
    );
  }

  it("deve aceitar o valor digitado e mostrar a máscara pt-BR fora do foco", async () => {
    const user = userEvent.setup();
    const aoMudar = vi.fn();
    renderSimples(<Controlado aoMudar={aoMudar} />);
    const campo = screen.getByRole("textbox", { name: "LMI" });
    expect(campo).toHaveAttribute("inputmode", "decimal");
    await user.type(campo, "100000x");
    expect(campo).toHaveValue("100000");
    expect(aoMudar).toHaveBeenLastCalledWith(100000);
    await user.tab();
    expect(campo).toHaveValue("100.000,00");
  });
});

const OPCOES = [
  { valor: "200", rotulo: "Reboque até 200 km" },
  { valor: "400", rotulo: "Reboque até 400 km" },
  { valor: "ilimitado", rotulo: "Reboque ilimitado", desabilitada: true },
];

describe("Select", () => {
  it("deve abrir pelo teclado e escolher com setas e Enter", async () => {
    const user = userEvent.setup();
    const aoMudar = vi.fn();
    renderSimples(
      <FormField rotulo="Assistência">
        {(campo) => <Select {...campo} opcoes={OPCOES} valor="200" aoMudar={aoMudar} />}
      </FormField>,
    );
    const gatilho = screen.getByRole("combobox", { name: "Assistência" });
    gatilho.focus();
    await user.keyboard("{Enter}");
    const lista = await screen.findByRole("listbox");
    expect(within(lista).getByRole("option", { name: "Reboque ilimitado" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    await user.keyboard("{ArrowDown}{Enter}");
    expect(aoMudar).toHaveBeenCalledWith("400");
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <FormField rotulo="Assistência">
        {(campo) => <Select {...campo} opcoes={OPCOES} valor="200" />}
      </FormField>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Switch", () => {
  it("deve alternar com clique e com Espaço", async () => {
    const user = userEvent.setup();
    const aoMudar = vi.fn();
    renderSimples(
      <>
        <label htmlFor="morais">Danos morais</label>
        <Switch id="morais" ligado={false} aoMudar={aoMudar} />
      </>,
    );
    const chave = screen.getByRole("switch", { name: "Danos morais" });
    await user.click(chave);
    chave.focus();
    await user.keyboard(" ");
    expect(aoMudar).toHaveBeenCalledTimes(2);
    expect(aoMudar).toHaveBeenNthCalledWith(1, true);
  });

  it("deve manter a obrigatória ligada, travada e em cor plena, com o selo no nome", () => {
    renderSimples(
      <>
        <label htmlFor="casco">
          Casco <SeloObrigatoria />
        </label>
        <Switch id="casco" obrigatorio />
        <label htmlFor="comum">Comum</label>
        <Switch id="comum" ligado desabilitado />
      </>,
    );
    const chave = screen.getByRole("switch", { name: "Casco Obrigatória" });
    expect(chave).toBeChecked();
    expect(chave).toBeDisabled();
    // Esmaecer a obrigatória a deixava com cara de desligada; só o desabilitado comum esmaece.
    expect(chave.className).not.toMatch(/opacity/);
    expect(screen.getByRole("switch", { name: "Comum" }).className).toMatch(/opacity-50/);
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <>
        <label htmlFor="vidros">Vidros</label>
        <Switch id="vidros" ligado />
      </>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("SegmentedControl", () => {
  it("deve trocar a opção com as setas (semântica de radiogroup)", async () => {
    const user = userEvent.setup();
    function Controlado() {
      const [valor, setValor] = useState<"1" | "3" | "10">("1");
      return (
        <SegmentedControl
          rotulo="Parcelamento"
          opcoes={[
            { valor: "1", rotulo: "1×" },
            { valor: "3", rotulo: "3×" },
            { valor: "10", rotulo: "10×" },
          ]}
          valor={valor}
          aoMudar={setValor}
        />
      );
    }
    renderSimples(<Controlado />);
    expect(screen.getByRole("radiogroup", { name: "Parcelamento" })).toBeInTheDocument();
    await user.tab();
    expect(screen.getByRole("radio", { name: "1×" })).toHaveFocus();
    // O Radix move o foco num setTimeout e só marca se a seta ainda estiver pressionada
    // (como num teclado real): segura a tecla, espera o foco andar e solta.
    await user.keyboard("{ArrowRight>}");
    await waitFor(() => expect(screen.getByRole("radio", { name: "3×" })).toBeChecked());
    await user.keyboard("{/ArrowRight}{ArrowRight>}");
    await waitFor(() => expect(screen.getByRole("radio", { name: "10×" })).toBeChecked());
    await user.keyboard("{/ArrowRight}");
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <SegmentedControl
        rotulo="Tipo de imóvel"
        opcoes={[
          { valor: "casa", rotulo: "Casa" },
          { valor: "apto", rotulo: "Apartamento" },
        ]}
        valor="casa"
        aoMudar={vi.fn()}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("Checkbox", () => {
  it("deve marcar pelo rótulo e pelo Espaço", async () => {
    const user = userEvent.setup();
    const aoMudar = vi.fn();
    renderSimples(<Checkbox rotulo="Manter conectado" marcado={false} aoMudar={aoMudar} />);
    await user.click(screen.getByText("Manter conectado"));
    screen.getByRole("checkbox", { name: "Manter conectado" }).focus();
    await user.keyboard(" ");
    expect(aoMudar).toHaveBeenCalledTimes(2);
    expect(aoMudar).toHaveBeenCalledWith(true);
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(<Checkbox rotulo="Aceito os termos" marcado />);
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("GrupoCartoesEscolha", () => {
  const OPCOES_CARTAO = [
    { valor: "prazo", titulo: "Pedido do segurado", descricao: "Prazo curto: retém 20%" },
    { valor: "pro-rata", titulo: "Iniciativa da seguradora", descricao: "Pro rata die" },
    {
      valor: "inadimplencia",
      titulo: "Inadimplência",
      indisponivel: "Sem parcela vencida em aberto",
    },
  ] as const;

  it("deve escolher pelo clique, marcar com check e explicar a opção indisponível", async () => {
    const user = userEvent.setup();
    const aoMudar = vi.fn();
    renderSimples(
      <GrupoCartoesEscolha rotulo="Motivo" opcoes={OPCOES_CARTAO} valor="" aoMudar={aoMudar} />,
    );
    expect(screen.getByRole("radiogroup", { name: "Motivo" })).toBeInTheDocument();
    const prazo = screen.getByRole("radio", { name: "Pedido do segurado" });
    expect(prazo).toHaveAccessibleDescription("Prazo curto: retém 20%");
    await user.click(prazo);
    expect(aoMudar).toHaveBeenCalledWith("prazo");
    const indisponivel = screen.getByRole("radio", { name: "Inadimplência" });
    expect(indisponivel).toBeDisabled();
    expect(indisponivel).toHaveAccessibleDescription("Sem parcela vencida em aberto");
  });

  it("não deve ter violações de acessibilidade com uma opção escolhida", async () => {
    const { container } = renderSimples(
      <GrupoCartoesEscolha
        rotulo="Motivo"
        opcoes={OPCOES_CARTAO}
        valor="prazo"
        aoMudar={vi.fn()}
      />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

describe("ChipFiltro e CampoBusca", () => {
  it("deve alternar o filtro com aria-pressed e mostrar a contagem", async () => {
    const user = userEvent.setup();
    function Controlado() {
      const [ativo, setAtivo] = useState(false);
      return (
        <ChipFiltro ativo={ativo} contador={12} onClick={() => setAtivo((v) => !v)}>
          Ativa
        </ChipFiltro>
      );
    }
    renderSimples(<Controlado />);
    const chip = screen.getByRole("button", { name: "Ativa, 12" });
    expect(chip).toHaveAttribute("aria-pressed", "false");
    await user.click(chip);
    expect(chip).toHaveAttribute("aria-pressed", "true");
  });

  it("deve buscar só 300 ms depois da última tecla", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    const aoBuscar = vi.fn();
    renderSimples(<CampoBusca rotulo="Buscar apólices" aoBuscar={aoBuscar} />);
    await vi.advanceTimersByTimeAsync(10);
    aoBuscar.mockClear();
    await user.type(screen.getByRole("searchbox", { name: "Buscar apólices" }), "RFN");
    expect(aoBuscar).not.toHaveBeenCalledWith("RFN");
    await vi.advanceTimersByTimeAsync(300);
    expect(aoBuscar).toHaveBeenLastCalledWith("RFN");
    expect(aoBuscar).toHaveBeenCalledTimes(1);
    vi.useRealTimers();
  });

  it("não deve ter violações de acessibilidade", async () => {
    const { container } = renderSimples(
      <div>
        <CampoBusca rotulo="Buscar apólices" aoBuscar={vi.fn()} />
        <ChipFiltro ativo contador={3}>
          Ativa
        </ChipFiltro>
      </div>,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});

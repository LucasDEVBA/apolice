import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { db } from "@/mocks/banco";
import { tarifar } from "@/mocks/regras";
import { renderApp } from "@/test/app";
import { acharToast, textoMoeda } from "@/test/interacoes";
import { axe } from "@/test/utilitarios";
import { hojeEmBrasilia } from "@/ui/utils/datas";

const SEGURADO = {
  nome: "Lucas Andrade",
  cpf: "52998224725",
  email: "lucas.andrade@exemplo.com.br",
  dataNascimento: "1985-04-12",
};
const ANO = Number(hojeEmBrasilia().slice(0, 4)) - 3;

function totalEsperado(coberturas: { codigo: string; lmi: number | null }[]): string {
  const produto = db().produtos.find((p) => p.codigo === "AUTO")!;
  const resultado = tarifar(
    produto,
    SEGURADO,
    {
      produto: "AUTO",
      valorVeiculo: 141_900,
      anoFabricacao: ANO,
      cepPernoite: "41820021",
      idadeCondutor: 41,
      usoComercial: false,
      classeBonus: 0,
    },
    coberturas,
    hojeEmBrasilia(),
  );
  return textoMoeda(resultado.premioTotal);
}

/** Valores guardados pelo autosave, de qualquer usuário. */
function rascunhosGuardados(): string[] {
  return Object.keys(window.sessionStorage)
    .filter((chave) => chave.startsWith("apolice.rascunho-cotacao"))
    .map((chave) => window.sessionStorage.getItem(chave) ?? "");
}

const SEM_LANDMARKS_DUPLICADOS = {
  rules: {
    "landmark-unique": { enabled: false },
    "landmark-no-duplicate-banner": { enabled: false },
  },
};

describe("Nova cotação", () => {
  it("deve percorrer as 5 etapas, simular ao vivo e criar a cotação", async () => {
    const user = userEvent.setup({ delay: null });
    const { router } = renderApp("/cotacoes/nova");

    await user.click(await screen.findByRole("radio", { name: "Seguro Auto" }));
    await user.click(screen.getByRole("button", { name: "Continuar para Segurado" }));

    await user.type(await screen.findByRole("textbox", { name: /Nome completo/ }), SEGURADO.nome);
    await user.type(screen.getByRole("textbox", { name: /CPF/ }), "11111111111");
    await user.type(screen.getByRole("textbox", { name: /E-mail/ }), SEGURADO.email);
    fireEvent.change(screen.getByLabelText(/Data de nascimento/), {
      target: { value: SEGURADO.dataNascimento },
    });
    await user.click(screen.getByRole("button", { name: "Continuar para Risco" }));
    expect(await screen.findByText("CPF inválido: confira os 11 dígitos.")).toBeInTheDocument();

    const cpf = screen.getByRole("textbox", { name: /CPF/ });
    await user.clear(cpf);
    await user.type(cpf, SEGURADO.cpf);
    expect(cpf).toHaveValue("529.982.247-25");
    await user.click(screen.getByRole("button", { name: "Continuar para Risco" }));

    await user.type(await screen.findByRole("textbox", { name: /Valor do veículo/ }), "141900");
    await user.type(screen.getByRole("textbox", { name: /Ano de fabricação/ }), String(ANO));
    await user.type(screen.getByRole("textbox", { name: /CEP de pernoite/ }), "41820021");
    await user.type(screen.getByRole("textbox", { name: /Idade do condutor/ }), "41");
    await user.click(screen.getByRole("button", { name: "Continuar para Coberturas" }));

    // Obrigatórias (Casco e RC danos materiais com LMI sugerido de R$ 100 mil) já ligadas.
    const base = [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 100_000 },
    ];
    expect((await screen.findAllByText(totalEsperado(base))).length).toBeGreaterThan(0);
    expect(screen.getByText("Região de pernoite: BA/SE")).toBeInTheDocument();

    await user.click(await screen.findByRole("switch", { name: "Vidros, faróis e retrovisores" }));
    const comVidros = totalEsperado([...base, { codigo: "VIDROS", lmi: null }]);
    expect((await screen.findAllByText(comVidros, {}, { timeout: 10_000 })).length).toBeGreaterThan(
      0,
    );
    expect(await screen.findByText(/Rascunho salvo às/)).toBeInTheDocument();
    expect(await axe(document.body, SEM_LANDMARKS_DUPLICADOS)).toHaveNoViolations();

    await user.click(screen.getByRole("button", { name: "Revisar cotação" }));
    expect(await screen.findByRole("heading", { name: "Revisão" })).toBeInTheDocument();
    expect(screen.getByText("***.982.247-**")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Criar cotação" }));

    expect(await acharToast("Cotação criada")).toBeInTheDocument();
    expect(
      await screen.findByRole("heading", { name: `Cotação de ${SEGURADO.nome}`, level: 1 }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(router.state.location.pathname).toMatch(/^\/cotacoes\/[0-9a-f-]{36}$/),
    );
    expect(rascunhosGuardados()).toEqual([]);
  }, 90_000);

  it("deve levar o foco ao título da etapa e tirar o erro do campo corrigido antes do clique", async () => {
    const user = userEvent.setup({ delay: null });
    renderApp("/cotacoes/nova");
    await user.click(await screen.findByRole("radio", { name: "Seguro Auto" }));
    await user.click(screen.getByRole("button", { name: "Continuar para Segurado" }));
    await waitFor(() =>
      expect(screen.getByRole("heading", { name: "Segurado", level: 2 })).toHaveFocus(),
    );

    await user.type(screen.getByRole("textbox", { name: /Nome completo/ }), SEGURADO.nome);
    await user.type(screen.getByRole("textbox", { name: /E-mail/ }), SEGURADO.email);
    fireEvent.change(screen.getByLabelText(/Data de nascimento/), {
      target: { value: SEGURADO.dataNascimento },
    });
    await user.click(screen.getByRole("button", { name: "Continuar para Risco" }));
    expect(await screen.findByText("Informe o CPF do segurado.")).toBeInTheDocument();

    // Corrigido o campo (ainda focado), o erro some na digitação: o rodapé não pula no clique.
    const cpf = screen.getByRole("textbox", { name: /CPF/ });
    await user.click(cpf);
    await user.type(cpf, SEGURADO.cpf);
    expect(cpf).toHaveFocus();
    await waitFor(() =>
      expect(screen.queryByText("Informe o CPF do segurado.")).not.toBeInTheDocument(),
    );
    expect(screen.queryByText("CPF inválido: confira os 11 dígitos.")).not.toBeInTheDocument();

    // Enter dentro do campo avança; o campo some e o foco vai ao título da etapa nova.
    await user.keyboard("{Enter}");
    await waitFor(() =>
      expect(screen.getByRole("heading", { name: "Risco", level: 2 })).toHaveFocus(),
    );
    await user.click(screen.getByRole("button", { name: "Voltar para Segurado" }));
    await waitFor(() =>
      expect(screen.getByRole("heading", { name: "Segurado", level: 2 })).toHaveFocus(),
    );
  });

  it("não deve mostrar o rascunho de um usuário depois da troca de persona", async () => {
    const user = userEvent.setup({ delay: null });
    const { router } = renderApp("/cotacoes/nova");
    await user.click(await screen.findByRole("radio", { name: "Seguro Auto" }));
    await user.click(screen.getByRole("button", { name: "Continuar para Segurado" }));
    await user.type(await screen.findByRole("textbox", { name: /Nome completo/ }), SEGURADO.nome);
    await user.type(screen.getByRole("textbox", { name: /CPF/ }), SEGURADO.cpf);
    await waitFor(() => expect(rascunhosGuardados().join()).toContain(SEGURADO.nome));

    const gatilho = screen.getAllByRole("button", { name: "Trocar persona ou sair" })[0]!;
    gatilho.focus();
    await user.keyboard("{Enter}");
    await user.click(await screen.findByRole("menuitemradio", { name: /Bruno Lima/ }));
    expect(await acharToast("Agora você é Bruno Lima")).toBeInTheDocument();
    // Passado o tempo do autosave: nada foi regravado com os dados da Ana na sessão do Bruno.
    await new Promise((resolver) => setTimeout(resolver, 800));
    expect(rascunhosGuardados()).toEqual([]);

    await router.navigate("/cotacoes/nova?etapa=2");
    expect(await screen.findByRole("heading", { name: "Produto", level: 2 })).toBeInTheDocument();
    expect(screen.queryByDisplayValue(SEGURADO.nome)).not.toBeInTheDocument();
    expect(screen.queryByDisplayValue("529.982.247-25")).not.toBeInTheDocument();
  });

  it("deve explicar a faixa do produto ao validar os dados de risco", async () => {
    const user = userEvent.setup({ delay: null });
    renderApp("/cotacoes/nova");
    await user.click(await screen.findByRole("radio", { name: "Seguro Residencial" }));
    await user.click(screen.getByRole("button", { name: "Continuar para Segurado" }));
    await user.type(
      await screen.findByRole("textbox", { name: /Nome completo/ }),
      "Ana Paula Reis",
    );
    await user.type(screen.getByRole("textbox", { name: /CPF/ }), SEGURADO.cpf);
    await user.type(screen.getByRole("textbox", { name: /E-mail/ }), "ana.reis@exemplo.com.br");
    fireEvent.change(screen.getByLabelText(/Data de nascimento/), {
      target: { value: "1990-01-01" },
    });
    await user.click(screen.getByRole("button", { name: "Continuar para Risco" }));
    await user.type(await screen.findByRole("textbox", { name: /Valor do imóvel/ }), "10000");
    await user.type(screen.getByRole("textbox", { name: /CEP do imóvel/ }), "4015");
    await user.click(screen.getByRole("button", { name: "Continuar para Coberturas" }));
    expect(
      await screen.findByText("Informe um valor entre R$ 50.000,00 e R$ 5.000.000,00."),
    ).toBeInTheDocument();
    expect(screen.getByText("Informe o CEP com 8 dígitos.")).toBeInTheDocument();
  });

  it("deve bloquear a nova cotação para o perfil Analista com a página 403", async () => {
    renderApp("/cotacoes/nova", { persona: "analista" });
    expect(
      await screen.findByRole("heading", { name: "Acesso restrito", level: 1 }),
    ).toBeInTheDocument();
    expect(screen.getByText("Disponível para o perfil Corretor ou Admin")).toBeInTheDocument();
  });
});

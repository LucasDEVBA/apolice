import { expect, test, type Page } from "@playwright/test";

import { entrarComo, sair } from "./apoio";

/**
 * Fluxo principal do SPEC §10 contra a stack real (API + worker + banco): corretora cota um Auto,
 * aceita, emite, vê a apólice e as boas-vindas do worker, abre um sinistro; a analista analisa,
 * aprova e registra o pagamento. Cada execução usa um segurado novo (nome com carimbo de tempo).
 */

const CPF_VALIDO = "52998224725";
const ESPERA_WORKER_MS = 30_000;

function hojeEmBrasilia(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "America/Sao_Paulo" }).format(new Date());
}

async function continuar(page: Page, para: string) {
  await page.getByRole("button", { name: `Continuar para ${para}` }).click();
}

async function confirmarNoDialogo(page: Page, titulo: string, acao: string) {
  const dialogo = page.getByRole("dialog", { name: titulo });
  await expect(dialogo).toBeVisible();
  await dialogo.getByRole("button", { name: acao }).click();
  await expect(dialogo).toBeHidden();
}

async function escolherOpcao(page: Page, campo: ReturnType<Page["getByRole"]>, opcao: RegExp) {
  await campo.click();
  await page.getByRole("option", { name: opcao }).click();
}

test("fluxo principal: cotação Auto → emissão → sinistro → regulação → pagamento", async ({
  page,
  isMobile,
}) => {
  test.skip(isMobile, "O fluxo completo roda no desktop; o mobile está coberto em acessibilidade.");
  test.setTimeout(180_000);
  const segurado = `Segurado E2E ${String(Date.now())}`;
  const ano = Number(hojeEmBrasilia().slice(0, 4)) - 3;

  await test.step("corretora cria a cotação Auto pelo assistente", async () => {
    await entrarComo(page, "Ana Ribeiro");
    await page.getByRole("link", { name: "Nova cotação" }).first().click();
    await expect(page.getByRole("heading", { level: 1, name: "Nova cotação" })).toBeVisible();

    await page.getByRole("radio", { name: /Auto/ }).click();
    await continuar(page, "Segurado");
    await page.getByRole("textbox", { name: /Nome completo/ }).fill(segurado);
    await page.getByRole("textbox", { name: /CPF/ }).fill(CPF_VALIDO);
    await page.getByRole("textbox", { name: /E-mail/ }).fill("segurado.e2e@exemplo.com.br");
    await page.getByLabel(/Data de nascimento/).fill("1985-04-12");
    await continuar(page, "Risco");

    await page.getByRole("textbox", { name: /Valor do veículo/ }).fill("141900");
    await page.getByRole("textbox", { name: /Ano de fabricação/ }).fill(String(ano));
    await page.getByRole("textbox", { name: /CEP de pernoite/ }).fill("41820021");
    await page.getByRole("textbox", { name: /Idade do condutor/ }).fill("41");
    await continuar(page, "Coberturas");

    const painel = page.getByRole("complementary", { name: "Cálculo do prêmio" });
    await expect(painel.getByText(/Ao vivo/)).toBeVisible();
    await page.getByRole("switch", { name: /Vidros/ }).click();
    await expect(painel.getByText(/Vidros/)).toBeVisible();

    await page.getByRole("button", { name: "Revisar cotação" }).click();
    await page.getByRole("button", { name: "Criar cotação" }).click();
    await expect(
      page.getByRole("heading", { level: 1, name: `Cotação de ${segurado}` }),
    ).toBeVisible();
  });

  await test.step("aceita a cotação e emite a apólice", async () => {
    await page.getByRole("button", { name: "Aceitar cotação" }).click();
    await confirmarNoDialogo(page, "Aceitar cotação?", "Aceitar cotação");
    await page.getByRole("button", { name: "Emitir apólice" }).click();
    const dialogo = page.getByRole("dialog", { name: "Emitir apólice" });
    await expect(dialogo.getByLabel(/Início da vigência/)).toHaveValue(hojeEmBrasilia());
    await dialogo.getByRole("button", { name: "Emitir apólice" }).click();
    await expect(page.getByRole("heading", { level: 1, name: /^AUTO-\d{4}-\d{6}$/ })).toBeVisible();
  });

  const numeroApolice =
    (await page.getByRole("heading", { level: 1, name: /^AUTO-/ }).textContent()) ?? "";

  await test.step("vê a apólice e as boas-vindas enviadas pelo worker", async () => {
    await expect(page.getByText(segurado).first()).toBeVisible();
    await page.getByRole("tab", { name: /Comunicações/ }).click();
    await expect(
      page.getByText(`Bem-vindo(a)! Sua apólice ${numeroApolice} foi emitida`).first(),
    ).toBeVisible({ timeout: ESPERA_WORKER_MS });
  });

  let urlSinistro = "";
  await test.step("abre um sinistro na cobertura de Casco", async () => {
    await page.getByRole("button", { name: "Abrir sinistro" }).first().click();
    const dialogo = page.getByRole("dialog", {
      name: `Abrir sinistro na apólice ${numeroApolice}`,
    });
    await escolherOpcao(
      page,
      dialogo.getByRole("combobox", { name: /Cobertura afetada/ }),
      /Colisão/,
    );
    await dialogo.getByLabel(/Data da ocorrência/).fill(hojeEmBrasilia());
    await dialogo
      .getByRole("textbox", { name: /Descrição/ })
      .fill("Colisão lateral em estacionamento, porta traseira amassada e retrovisor quebrado.");
    await dialogo.getByRole("textbox", { name: /Valor reclamado/ }).fill("5400");
    await dialogo.getByRole("button", { name: "Abrir sinistro" }).click();
    await expect(page.getByRole("heading", { level: 1, name: /^Sinistro SIN-/ })).toBeVisible();
    urlSinistro = new URL(page.url()).pathname;
  });

  await test.step("analista inicia a análise, aprova e registra o pagamento", async () => {
    await sair(page);
    await entrarComo(page, "Carla Souza");
    await page.goto(urlSinistro);
    await expect(page.getByRole("heading", { level: 1, name: /^Sinistro SIN-/ })).toBeVisible();
    await expect(page.getByText(/Triado em/)).toBeVisible({ timeout: ESPERA_WORKER_MS });

    await page.getByRole("button", { name: "Iniciar análise" }).click();
    await confirmarNoDialogo(page, "Iniciar a análise?", "Iniciar análise");

    await page.getByRole("button", { name: "Aprovar", exact: true }).click();
    const aprovar = page.getByRole("dialog", { name: /^Aprovar o sinistro/ });
    await aprovar.getByRole("button", { name: "Aprovar indenização" }).click();
    await expect(aprovar).toBeHidden();

    await page.getByRole("button", { name: "Registrar pagamento" }).click();
    await confirmarNoDialogo(page, "Registrar o pagamento?", "Registrar pagamento");
    const historico = page.getByRole("list", { name: "Histórico do sinistro" });
    await expect(historico.getByText("Pagamento registrado")).toBeVisible();
  });
});

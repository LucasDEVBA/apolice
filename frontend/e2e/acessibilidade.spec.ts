import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

import { entrarComo } from "./apoio";

// Telas principais contra a API real; o fluxo do SPEC §10 está em fluxo-principal.spec.ts.
const TEMAS = ["light", "dark"] as const;
const PAGINAS = [
  { rota: "/login", titulo: "Entrar" },
  { rota: "/", titulo: "Visão da carteira" },
  { rota: "/cotacoes", titulo: "Cotações" },
  { rota: "/apolices", titulo: "Apólices" },
  { rota: "/sinistros", titulo: "Sinistros" },
  { rota: "/cotacoes/nova", titulo: "Nova cotação" },
  { rota: "/sobre", titulo: "Sobre o projeto" },
] as const;

for (const tema of TEMAS) {
  test.describe(`tema ${tema}`, () => {
    test.use({ colorScheme: tema });

    for (const pagina of PAGINAS) {
      test(`${pagina.rota} não tem violações WCAG 2.1 AA nem rolagem horizontal`, async ({
        page,
      }) => {
        if (pagina.rota !== "/login") await entrarComo(page);
        await page.goto(pagina.rota);
        await expect(page.getByRole("heading", { level: 1, name: pagina.titulo })).toBeVisible();
        const resultado = await new AxeBuilder({ page })
          .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
          .analyze();
        expect(resultado.violations).toEqual([]);
        const larguraDoDocumento = await page.evaluate(() => document.documentElement.scrollWidth);
        expect(larguraDoDocumento).toBeLessThanOrEqual(
          page.viewportSize()?.width ?? Number.POSITIVE_INFINITY,
        );
      });
    }
  });
}

test("o primeiro Tab leva ao skip link, que pula para o conteúdo", async ({ page }) => {
  await entrarComo(page);
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1, name: "Visão da carteira" })).toBeVisible();
  await page.keyboard.press("Tab");
  const pular = page.getByRole("link", { name: "Pular para o conteúdo" });
  await expect(pular).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("main#conteudo")).toBeFocused();
});

test("a troca de tema persiste entre recarregamentos", async ({ page, isMobile }) => {
  test.skip(isMobile, "No mobile o tema fica no drawer; o fluxo é o mesmo.");
  await entrarComo(page);
  await page.getByRole("button", { name: /Aparência/ }).click();
  await page.getByRole("menuitemradio", { name: "Escuro" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
});

const ETIQUETAS_WCAG = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

test.describe("sobreposições abertas", () => {
  test("a paleta de comandos não tem violações e devolve o foco ao fechar", async ({ page }) => {
    await entrarComo(page);
    await expect(page.getByRole("heading", { level: 1, name: "Visão da carteira" })).toBeVisible();
    await page.keyboard.press("ControlOrMeta+k");
    await expect(page.getByRole("combobox", { name: "Buscar comando ou página" })).toBeFocused();
    const resultado = await new AxeBuilder({ page }).withTags(ETIQUETAS_WCAG).analyze();
    expect(resultado.violations).toEqual([]);
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog")).toHaveCount(0);
  });

  test("o drawer do mobile não tem violações, fecha pelo botão e devolve o foco ao ☰", async ({
    page,
    isMobile,
  }) => {
    test.skip(!isMobile, "O drawer pelo ☰ da top bar é do mobile.");
    await entrarComo(page);
    const abrir = page.getByRole("button", { name: "Abrir menu" });
    await abrir.click();
    const drawer = page.getByRole("dialog", { name: "Menu" });
    await expect(drawer).toBeVisible();
    const resultado = await new AxeBuilder({ page }).withTags(ETIQUETAS_WCAG).analyze();
    expect(resultado.violations).toEqual([]);
    await drawer.getByRole("button", { name: "Fechar menu" }).click();
    await expect(abrir).toBeFocused();
  });

  test("o menu de aparência não tem violações", async ({ page, isMobile }) => {
    test.skip(isMobile, "No mobile o tema fica no drawer.");
    await entrarComo(page);
    await page.getByRole("button", { name: /Aparência/ }).click();
    await expect(page.getByRole("menu")).toBeVisible();
    const resultado = await new AxeBuilder({ page }).withTags(ETIQUETAS_WCAG).analyze();
    expect(resultado.violations).toEqual([]);
  });
});

// data-theme forçado contra o tema do sistema: é quando o light-dark() depende do color-scheme
// do :root[data-theme], e não do prefers-color-scheme.
for (const [preferencia, sistema] of [
  ["escuro", "light"],
  ["claro", "dark"],
] as const) {
  test(`tema ${preferencia} forçado com o sistema em ${sistema} não tem violações`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme: sistema });
    await page.addInitScript((valor) => {
      window.localStorage.setItem("apolice.tema", valor);
    }, preferencia);
    await entrarComo(page);
    await expect(page.locator("html")).toHaveAttribute(
      "data-theme",
      preferencia === "escuro" ? "dark" : "light",
    );
    const resultado = await new AxeBuilder({ page }).withTags(ETIQUETAS_WCAG).analyze();
    expect(resultado.violations).toEqual([]);
  });
}

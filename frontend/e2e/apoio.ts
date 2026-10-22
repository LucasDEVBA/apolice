import { expect, type Page } from "@playwright/test";

export type PersonaDemo = "Ana Ribeiro" | "Bruno Lima" | "Carla Souza" | "Diego Martins";

/**
 * Entra pela tela de login com uma persona de demonstração (POST /auth/demo). O cookie de refresh
 * fica no contexto do navegador: um page.goto depois disso restaura a sessão sozinho.
 */
export async function entrarComo(page: Page, persona: PersonaDemo = "Ana Ribeiro"): Promise<void> {
  await page.goto("/login");
  await page.getByRole("button", { name: new RegExp(persona) }).click();
  // Depois do login o app volta para a página de onde a sessão saiu (ou para o dashboard): o que
  // importa é ter saído do /login com o shell autenticado.
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toBeVisible();
}

/** Sai pelo menu do usuário no rodapé da navegação (desktop). */
export async function sair(page: Page): Promise<void> {
  await page.getByRole("button", { name: "Trocar persona ou sair" }).first().click();
  await page.getByRole("menuitem", { name: "Sair" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Entrar" })).toBeVisible();
}

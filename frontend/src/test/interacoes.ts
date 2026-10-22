import { screen, waitFor, within } from "@testing-library/react";
import type { UserEvent } from "@testing-library/user-event";

/** Abre o Radix Select pelo teclado e escolhe a opção pelo nome (sem depender de pointer events). */
export async function escolherNoSelect(
  user: UserEvent,
  campo: HTMLElement,
  opcao: string | RegExp,
) {
  campo.focus();
  await user.keyboard("{Enter}");
  const lista = await screen.findByRole("listbox");
  await user.click(within(lista).getByRole("option", { name: opcao }));
}

/** Toast do sonner: o mesmo texto pode estar também na página (ex.: na linha do tempo). */
export async function acharToast(texto: string | RegExp): Promise<HTMLElement> {
  return waitFor(
    () => {
      const toast = screen.queryAllByText(texto).find((el) => el.closest("[data-sonner-toast]"));
      if (!toast) throw new Error(`Toast não encontrado: ${String(texto)}`);
      return toast;
    },
    { timeout: 10_000 },
  );
}

/** Moeda como o Testing Library compara: o NBSP do Intl vira espaço comum na normalização. */
export function textoMoeda(valor: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" })
    .format(valor)
    .replace(/\s/g, " ");
}

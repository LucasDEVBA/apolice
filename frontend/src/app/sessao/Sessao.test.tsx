import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import {
  db,
  definirSessaoRefresh,
  expirarTokensDeAcesso,
  injetarFalha,
  usuarioPorPersona,
} from "@/mocks/banco";
import { renderApp } from "@/test/app";
import { acharToast } from "@/test/interacoes";

describe("sessão e erros da API", () => {
  it("deve renovar o token de forma transparente quando o access token expira", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByText("Escopo: sua carteira");
    const renovacoesAntes = db().contadores.refresh;
    expirarTokensDeAcesso();
    await user.click(screen.getAllByRole("link", { name: /^Apólices(, \d+)?$/ })[0]!);
    expect(await screen.findByRole("heading", { name: "Apólices", level: 1 })).toBeInTheDocument();
    expect((await screen.findAllByText("Rafael Nogueira Castro")).length).toBeGreaterThan(0);
    expect(db().contadores.refresh - renovacoesAntes).toBe(1);
  });

  it("deve voltar ao login com aviso quando o refresh também for recusado", async () => {
    const user = userEvent.setup();
    const { router } = renderApp("/");
    await screen.findByText("Escopo: sua carteira");
    expirarTokensDeAcesso();
    db().sessaoRefresh = null;
    await user.click(screen.getAllByRole("link", { name: /^Sinistros(, \d+)?$/ })[0]!);
    expect(await acharToast("Sua sessão expirou")).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe("/login"));
  });

  it("deve mostrar um único aviso de sessão expirada quando várias chamadas recebem 401", async () => {
    const { router } = renderApp("/apolices");
    expect(await screen.findByRole("heading", { name: "Apólices", level: 1 })).toBeInTheDocument();
    expirarTokensDeAcesso();
    db().sessaoRefresh = null;
    // O dashboard dispara várias consultas ao mesmo tempo: todas levam 401 e esperam o refresh.
    await router.navigate("/");
    await waitFor(() => expect(router.state.location.pathname).toBe("/login"));
    await acharToast("Sua sessão expirou");
    const avisos = screen
      .queryAllByText("Sua sessão expirou")
      .filter((el) => el.closest("[data-sonner-toast]"));
    expect(avisos).toHaveLength(1);
  });

  it("deve tratar como sessão nova quando a renovação devolve outro usuário (outra aba trocou)", async () => {
    const user = userEvent.setup();
    renderApp("/");
    await screen.findByText("Escopo: sua carteira");
    expect(screen.getAllByText(/atual: Ana Ribeiro/).length).toBeGreaterThan(0);
    // Outra aba entrou como Diego (Admin): o cookie de refresh, que é do navegador, agora é dele.
    definirSessaoRefresh(usuarioPorPersona("admin").id);
    expirarTokensDeAcesso();
    await user.click(screen.getAllByRole("link", { name: /^Apólices(, \d+)?$/ })[0]!);
    expect(await acharToast("A sessão desta aba mudou para Diego Martins")).toBeInTheDocument();
    // Shell, permissões e cache passam a ser do Diego (o Admin deixa de ser item restrito).
    expect((await screen.findAllByText(/atual: Diego Martins/)).length).toBeGreaterThan(0);
    expect(screen.queryByText(/atual: Ana Ribeiro/)).not.toBeInTheDocument();
    expect(screen.queryByText("Admin (restrito ao perfil Admin)")).not.toBeInTheDocument();
  });

  it("deve responder com a página de não encontrado para apólice de outro corretor (BOLA → 404)", async () => {
    const apoliceDaAna = db().apolices.find(
      (a) => a.corretorId === usuarioPorPersona("corretora").id,
    )!;
    renderApp(`/apolices/${apoliceDaAna.id}`, { persona: "corretor" });
    expect(
      await screen.findByRole("heading", { name: "Não encontramos a apólice" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/não faz parte da sua carteira/)).toBeInTheDocument();
    expect(screen.queryByText(apoliceDaAna.numero)).not.toBeInTheDocument();
  });

  it("deve mostrar o erro da lista com o ID de correlação copiável", async () => {
    injetarFalha({
      metodo: "GET",
      caminho: /\/api\/v1\/cotacoes$/,
      status: 500,
      codigo: "Comum.Falha",
      mensagem: "Banco indisponível.",
      correlationId: "corr-lista-123",
    });
    renderApp("/cotacoes");
    expect(
      await screen.findByRole("heading", { name: "Não foi possível carregar as cotações" }),
    ).toBeInTheDocument();
    expect(screen.getByText("corr-lista-123")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Copiar ID de correlação" })).toBeInTheDocument();
  });

  it("deve transformar o erro de uma ação em toast com o ID de correlação", async () => {
    const user = userEvent.setup();
    const aberto = db().sinistros.find((s) => s.status === "Aberto")!;
    injetarFalha({
      metodo: "POST",
      caminho: /\/analise$/,
      status: 409,
      codigo: "Concorrencia.Conflito",
      mensagem: "O registro foi alterado por outra pessoa. Recarregue e tente de novo.",
      correlationId: "corr-acao-456",
    });
    renderApp(`/sinistros/${aberto.id}`, { persona: "analista" });
    await user.click(await screen.findByRole("button", { name: "Iniciar análise" }));
    const dialogo = await screen.findByRole("dialog", { name: "Iniciar a análise?" });
    await user.click(within(dialogo).getByRole("button", { name: "Iniciar análise" }));
    expect(
      await acharToast("O registro foi alterado por outra pessoa. Recarregue e tente de novo."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Copiar ID corr-acao-456/ })).toBeInTheDocument();
  });
});

import { afterEach, describe, expect, it } from "vitest";

import {
  db,
  definirSessaoRefresh,
  expirarTokensDeAcesso,
  injetarFalha,
  usuarioPorPersona,
} from "@/mocks/banco";

import { listarProdutos } from "./endpoints/produtos";
import { obterFeatures } from "./endpoints/sistema";
import { ErroApi } from "./erros";
import { aoExpirarSessao, definirTokenDeAcesso, renovarSessao } from "./http";

describe("cliente HTTP", () => {
  afterEach(() => {
    Reflect.deleteProperty(navigator, "locks");
  });

  it("deve chamar endpoint anônimo sem token", async () => {
    await expect(obterFeatures()).resolves.toMatchObject({ loginDemo: true });
  });

  it("deve renovar a sessão uma única vez para várias chamadas com 401 e repetir cada uma", async () => {
    definirSessaoRefresh(usuarioPorPersona("corretora").id);
    await renovarSessao();
    expirarTokensDeAcesso();
    const antes = db().contadores.refresh;
    const resultados = await Promise.all([listarProdutos(), listarProdutos(), listarProdutos()]);
    expect(resultados.every((r) => r.length === 3)).toBe(true);
    expect(db().contadores.refresh - antes).toBe(1);
  });

  it("deve serializar o refresh entre abas pela trava do navegador (Web Locks)", async () => {
    definirSessaoRefresh(usuarioPorPersona("corretora").id);
    const nomes: string[] = [];
    // O jsdom não tem Web Locks: uma trava falsa registra o nome e executa a tarefa.
    Object.defineProperty(navigator, "locks", {
      configurable: true,
      value: {
        request: (nome: string, _opcoes: unknown, tarefa: () => Promise<unknown>) => {
          nomes.push(nome);
          return tarefa();
        },
      },
    });
    await expect(renovarSessao()).resolves.toMatchObject({ usuario: { nome: "Ana Ribeiro" } });
    expect(nomes).toEqual(["apolice-refresh"]);
  });

  it("deve encerrar a sessão local quando o refresh for recusado", async () => {
    definirTokenDeAcesso("token-invalido");
    let expirou = false;
    const parar = aoExpirarSessao(() => {
      expirou = true;
    });
    await expect(listarProdutos()).rejects.toMatchObject({ status: 401 });
    expect(expirou).toBe(true);
    parar();
  });

  it("não deve avisar sessão expirada quando não havia sessão (depois de sair)", async () => {
    definirTokenDeAcesso(null);
    let expirou = false;
    const parar = aoExpirarSessao(() => {
      expirou = true;
    });
    await expect(listarProdutos()).rejects.toMatchObject({ status: 401 });
    expect(expirou).toBe(false);
    parar();
  });

  it("deve transformar ProblemDetails em ErroApi com codigo e correlationId", async () => {
    definirSessaoRefresh(usuarioPorPersona("corretora").id);
    await renovarSessao();
    injetarFalha({
      metodo: "GET",
      caminho: /\/produtos$/,
      status: 500,
      codigo: "Comum.Falha",
      mensagem: "Falha simulada.",
      correlationId: "corr-teste-1",
    });
    const erro = await listarProdutos().catch((e: unknown) => e);
    expect(erro).toBeInstanceOf(ErroApi);
    expect(erro).toMatchObject({
      status: 500,
      codigo: "Comum.Falha",
      detalhe: "Falha simulada.",
      correlationId: "corr-teste-1",
    });
  });
});

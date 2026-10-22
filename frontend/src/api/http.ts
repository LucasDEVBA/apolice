import type { AuthResponse } from "./contratos";
import { ErroApi, erroDaResposta, erroDeRede } from "./erros";

/** Mesma origem em dev (proxy do Vite) e na Vercel (rewrite): cookie de refresh first-party. */
export const BASE_API = "/api/v1";
/** Defesa em profundidade contra CSRF exigida em /auth/refresh e /auth/logout (SPEC §7). */
export const CABECALHO_CSRF = { "X-Requested-With": "apolice-web" } as const;

const HTTP_SEM_CONTEUDO = 204;
const HTTP_NAO_AUTORIZADO = 401;
const TEMPO_LIMITE_MS = 20_000;

export type MetodoHttp = "GET" | "POST" | "PUT" | "DELETE";
export type ValorQuery = string | number | boolean | null | undefined;

export interface OpcoesRequisicao {
  metodo?: MetodoHttp;
  corpo?: unknown;
  query?: Readonly<Record<string, ValorQuery>>;
  cabecalhos?: Readonly<Record<string, string>>;
  sinal?: AbortSignal | undefined;
  /** Idempotency-Key (POST /apolices e POST /sinistros): uma chave por submissão. */
  chaveIdempotencia?: string | undefined;
  /** "blob" para o PDF; "vazia" para 204. */
  resposta?: "json" | "blob" | "vazia";
  /** Login, demo, refresh, features: sem Authorization e sem tentar renovar a sessão. */
  anonima?: boolean;
}

/* ---------------------------------------------------------------- sessão em memória */

// Access token só em memória (nunca localStorage): um XSS não o encontra num storage persistente.
let tokenDeAcesso: string | null = null;
let renovacaoEmAndamento: Promise<AuthResponse> | null = null;
const ouvintesDeRenovacao = new Set<(resposta: AuthResponse) => void>();
const ouvintesDeExpiracao = new Set<() => void>();

/** Nome da trava (Web Locks) que serializa o refresh entre as abas da mesma origem. */
const TRAVA_REFRESH = "apolice-refresh";
const CANAL_SESSAO = "apolice-sessao";
/** Renovação feita por outra aba há menos que isso ainda serve: o access token vale 15 min. */
const JANELA_RENOVACAO_COMPARTILHADA_MS = 30_000;

interface RenovacaoCompartilhada {
  resposta: AuthResponse;
  em: number;
}

type MensagemSessao =
  { tipo: "renovada"; resposta: AuthResponse; em: number } | { tipo: "encerrada" };

let ultimaRenovacaoDeOutraAba: RenovacaoCompartilhada | null = null;
let canalSessao: BroadcastChannel | null | undefined;

/**
 * Canal entre abas (criado no primeiro uso). Uma aba que renovou avisa as outras, que reaproveitam
 * o access token em vez de girar o refresh de novo. Sem BroadcastChannel, cada aba renova sozinha
 * (a trava ainda impede o reuso do refresh rotativo).
 */
function canal(): BroadcastChannel | null {
  if (canalSessao !== undefined) return canalSessao;
  if (typeof BroadcastChannel === "undefined") return (canalSessao = null);
  const novo = new BroadcastChannel(CANAL_SESSAO);
  novo.onmessage = (evento: MessageEvent<MensagemSessao>) => {
    const mensagem = evento.data;
    ultimaRenovacaoDeOutraAba =
      mensagem.tipo === "renovada" ? { resposta: mensagem.resposta, em: mensagem.em } : null;
  };
  // Node (testes) mantém o processo vivo com um canal aberto; o navegador não tem unref.
  (novo as BroadcastChannel & { unref?: () => void }).unref?.();
  canalSessao = novo;
  return novo;
}

function avisarOutrasAbas(mensagem: MensagemSessao): void {
  try {
    canal()?.postMessage(mensagem);
  } catch (erro) {
    // O aviso é otimização: sem ele a outra aba só renova por conta própria.
    console.warn({ nivel: "aviso", acao: "aviso_entre_abas_falhou", erro });
  }
}

/**
 * Executa sob a trava exclusiva entre abas. O refresh token é rotativo e reapresentá-lo revoga a
 * família inteira (SPEC §5): duas abas renovando juntas com o mesmo cookie derrubariam as duas.
 * Serializadas, a segunda já envia o cookie que a primeira acabou de receber.
 */
function comTravaEntreAbas<T>(tarefa: () => Promise<T>): Promise<T> {
  const travas = typeof navigator === "undefined" ? undefined : navigator.locks;
  if (!travas?.request) return tarefa();
  // O callback devolve Promise<T>; a tipagem do DOM embrulha de novo, mas request resolve com T.
  return travas.request(TRAVA_REFRESH, { mode: "exclusive" }, tarefa) as Promise<T>;
}

export function definirTokenDeAcesso(token: string | null): void {
  tokenDeAcesso = token;
}

export function temTokenDeAcesso(): boolean {
  return tokenDeAcesso !== null;
}

/** Logout ou novo login nesta aba: o token que as outras abas guardaram não serve mais. */
export function avisarFimDaSessao(): void {
  ultimaRenovacaoDeOutraAba = null;
  avisarOutrasAbas({ tipo: "encerrada" });
}

/** O contexto de sessão ouve as renovações (o usuário pode ter mudado de papel ou de pessoa). */
export function aoRenovarSessao(ouvinte: (resposta: AuthResponse) => void): () => void {
  ouvintesDeRenovacao.add(ouvinte);
  return () => ouvintesDeRenovacao.delete(ouvinte);
}

/** Refresh recusado no meio do uso: o contexto limpa a sessão e a rota volta ao login. */
export function aoExpirarSessao(ouvinte: () => void): () => void {
  ouvintesDeExpiracao.add(ouvinte);
  return () => ouvintesDeExpiracao.delete(ouvinte);
}

async function renovarNaTrava(pedidaEm: number): Promise<AuthResponse> {
  canal();
  const outra = ultimaRenovacaoDeOutraAba;
  // Outra aba renovou enquanto esta esperava a trava (ou há pouco): o cookie já girou, e o access
  // token que ela recebeu serve aqui também (se não for o mesmo que acabou de levar 401).
  if (
    outra &&
    outra.em >= pedidaEm - JANELA_RENOVACAO_COMPARTILHADA_MS &&
    outra.resposta.accessToken !== tokenDeAcesso
  ) {
    return outra.resposta;
  }
  const resposta = await requisitar<AuthResponse>("/auth/refresh", {
    metodo: "POST",
    cabecalhos: CABECALHO_CSRF,
    anonima: true,
  });
  avisarOutrasAbas({ tipo: "renovada", resposta, em: Date.now() });
  return resposta;
}

/**
 * Refresh single-flight: várias chamadas com 401 ao mesmo tempo (o dashboard dispara 4) esperam a
 * MESMA renovação, e a trava entre abas faz o mesmo para as outras abas. Se o refresh for recusado
 * com a sessão ativa, a sessão local termina UMA vez por renovação (um aviso, não um por chamada).
 */
export function renovarSessao(): Promise<AuthResponse> {
  renovacaoEmAndamento ??= (async () => {
    const pedidaEm = Date.now();
    try {
      const resposta = await comTravaEntreAbas(() => renovarNaTrava(pedidaEm));
      definirTokenDeAcesso(resposta.accessToken);
      for (const ouvinte of ouvintesDeRenovacao) ouvinte(resposta);
      return resposta;
    } catch (erro) {
      // Só o refresh recusado (401: expirado, revogado, reusado) encerra; queda de rede não. Sem
      // token (carga da página, ou depois de "Sair") não havia sessão a expirar.
      if (erro instanceof ErroApi && erro.naoAutorizado && tokenDeAcesso !== null) {
        encerrarSessaoLocal();
      }
      throw erro;
    } finally {
      renovacaoEmAndamento = null;
    }
  })();
  return renovacaoEmAndamento;
}

function encerrarSessaoLocal(): void {
  definirTokenDeAcesso(null);
  for (const ouvinte of ouvintesDeExpiracao) ouvinte();
}

/* ---------------------------------------------------------------- requisição */

function montarUrl(caminho: string, query: OpcoesRequisicao["query"]): string {
  // Absoluta a partir da origem da página: o fetch fora do navegador (testes) não resolve "/api".
  const url = new URL(`${BASE_API}${caminho}`, window.location.origin);
  for (const [chave, valor] of Object.entries(query ?? {})) {
    if (valor === undefined || valor === null || valor === "") continue;
    url.searchParams.set(chave, String(valor));
  }
  return url.toString();
}

function novoId(): string {
  return crypto.randomUUID();
}

function montarCabecalhos(opcoes: OpcoesRequisicao): Headers {
  const cabecalhos = new Headers(opcoes.cabecalhos);
  cabecalhos.set("Accept", opcoes.resposta === "blob" ? "application/pdf" : "application/json");
  cabecalhos.set("X-Correlation-Id", novoId());
  if (opcoes.corpo !== undefined) cabecalhos.set("Content-Type", "application/json");
  if (opcoes.chaveIdempotencia) cabecalhos.set("Idempotency-Key", opcoes.chaveIdempotencia);
  if (!opcoes.anonima && tokenDeAcesso) cabecalhos.set("Authorization", `Bearer ${tokenDeAcesso}`);
  return cabecalhos;
}

/** Junta o cancelamento de quem chamou (TanStack Query) com o tempo limite da requisição. */
function sinalComTempoLimite(sinal: AbortSignal | undefined): {
  sinal: AbortSignal;
  liberar: () => void;
} {
  const controlador = new AbortController();
  const abortar = () => controlador.abort();
  const timer = window.setTimeout(abortar, TEMPO_LIMITE_MS);
  if (sinal?.aborted) controlador.abort();
  sinal?.addEventListener("abort", abortar, { once: true });
  return {
    sinal: controlador.signal,
    liberar: () => {
      window.clearTimeout(timer);
      sinal?.removeEventListener("abort", abortar);
    },
  };
}

async function executar(caminho: string, opcoes: OpcoesRequisicao): Promise<Response> {
  const { sinal, liberar } = sinalComTempoLimite(opcoes.sinal);
  try {
    return await fetch(montarUrl(caminho, opcoes.query), {
      method: opcoes.metodo ?? "GET",
      headers: montarCabecalhos(opcoes),
      body: opcoes.corpo === undefined ? null : JSON.stringify(opcoes.corpo),
      // O cookie HttpOnly de refresh só vai em /auth (Path=/api/v1/auth); same-origin bastaria, mas
      // "include" mantém o comportamento se a API sair para outro subdomínio.
      credentials: "include",
      signal: sinal,
    });
  } catch (causa) {
    // Cancelamento pedido por quem chamou não é erro de rede: propaga o AbortError.
    if (opcoes.sinal?.aborted) throw causa;
    throw erroDeRede(causa);
  } finally {
    liberar();
  }
}

async function lerJson(resposta: Response): Promise<unknown> {
  const texto = await resposta.text();
  if (texto === "") return undefined;
  try {
    return JSON.parse(texto) as unknown;
  } catch {
    // Resposta não-JSON (proxy, HTML de erro): o ErroApi usa o título padrão do status.
    return undefined;
  }
}

async function interpretar<T>(resposta: Response, opcoes: OpcoesRequisicao): Promise<T> {
  if (!resposta.ok) {
    throw erroDaResposta(
      resposta.status,
      await lerJson(resposta),
      resposta.headers.get("X-Correlation-Id"),
    );
  }
  if (opcoes.resposta === "blob") return (await resposta.blob()) as T;
  if (resposta.status === HTTP_SEM_CONTEUDO || opcoes.resposta === "vazia") {
    return undefined as T;
  }
  return (await lerJson(resposta)) as T;
}

/**
 * Chamada à API v1. Em 401 com sessão, renova (single-flight) e repete a chamada original uma vez;
 * se a renovação falhar, renovarSessao encerra a sessão local (a rota protegida leva ao login).
 */
export async function requisitar<T>(caminho: string, opcoes: OpcoesRequisicao = {}): Promise<T> {
  const resposta = await executar(caminho, opcoes);
  if (resposta.status !== HTTP_NAO_AUTORIZADO || opcoes.anonima) {
    return interpretar<T>(resposta, opcoes);
  }
  await renovarSessao();
  return interpretar<T>(await executar(caminho, opcoes), opcoes);
}

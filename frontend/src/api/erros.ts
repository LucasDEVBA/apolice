import type { ProblemDetails } from "./contratos";

const PREFIXO_TIPO = "urn:apolice:problema:";
const HTTP_ERRO_SERVIDOR = 500;
const HTTP_NAO_AUTORIZADO = 401;
const HTTP_PROIBIDO = 403;
const HTTP_NAO_ENCONTRADO = 404;
const HTTP_MUITAS_REQUISICOES = 429;

/** Erro ao falar com a API: rede (status 0) ou resposta ProblemDetails (RFC 9457). */
export class ErroApi extends Error {
  override readonly name = "ErroApi";
  readonly status: number;
  /** "Cotacao.Expirada", "Comum.NaoEncontrado"… (extensão codigo ou sufixo do type). */
  readonly codigo: string | undefined;
  readonly titulo: string;
  readonly detalhe: string | undefined;
  /** Mensagens por campo, com o caminho já em camelCase ("segurado.cpf", "coberturas[1].lmi"). */
  readonly errosPorCampo: Readonly<Record<string, readonly string[]>>;
  readonly traceId: string | undefined;
  readonly correlationId: string | undefined;

  constructor(dados: {
    status: number;
    titulo: string;
    detalhe?: string | undefined;
    codigo?: string | undefined;
    errosPorCampo?: Record<string, readonly string[]>;
    traceId?: string | undefined;
    correlationId?: string | undefined;
    causa?: unknown;
  }) {
    super(dados.detalhe ?? dados.titulo, { cause: dados.causa });
    this.status = dados.status;
    this.titulo = dados.titulo;
    this.detalhe = dados.detalhe;
    this.codigo = dados.codigo;
    this.errosPorCampo = dados.errosPorCampo ?? {};
    this.traceId = dados.traceId;
    this.correlationId = dados.correlationId;
  }

  /** Sem resposta: offline, DNS, CORS, timeout. */
  get deRede(): boolean {
    return this.status === 0;
  }

  get naoEncontrado(): boolean {
    return this.status === HTTP_NAO_ENCONTRADO;
  }

  get naoAutorizado(): boolean {
    return this.status === HTTP_NAO_AUTORIZADO;
  }

  get proibido(): boolean {
    return this.status === HTTP_PROIBIDO;
  }

  get temErrosPorCampo(): boolean {
    return Object.keys(this.errosPorCampo).length > 0;
  }
}

/** Repetir vale a pena só para falha transitória: rede, 5xx e 429 (rate limit). */
export function ehErroTransitorio(erro: unknown): boolean {
  if (!(erro instanceof ErroApi)) return false;
  return (
    erro.deRede || erro.status >= HTTP_ERRO_SERVIDOR || erro.status === HTTP_MUITAS_REQUISICOES
  );
}

export function ehNaoEncontrado(erro: unknown): boolean {
  return erro instanceof ErroApi && erro.naoEncontrado;
}

/**
 * "Segurado.Cpf" → "segurado.cpf"; "Coberturas[1].Lmi" → "coberturas[1].lmi". O FluentValidation
 * devolve o caminho em PascalCase; o formulário fala camelCase como o JSON.
 */
export function normalizarCaminhoDeCampo(caminho: string): string {
  return caminho
    .split(".")
    .map((parte) => parte.charAt(0).toLowerCase() + parte.slice(1))
    .join(".");
}

function codigoDoTipo(tipo: string | undefined): string | undefined {
  return tipo?.startsWith(PREFIXO_TIPO) ? tipo.slice(PREFIXO_TIPO.length) : undefined;
}

const TITULOS_POR_STATUS: Record<number, string> = {
  400: "Dados inválidos",
  401: "Sessão expirada",
  403: "Sem permissão",
  404: "Não encontrado",
  409: "Registro alterado por outra pessoa",
  422: "Operação não permitida",
  429: "Muitas requisições",
};

function tituloPadrao(status: number): string {
  return (
    TITULOS_POR_STATUS[status] ??
    (status >= HTTP_ERRO_SERVIDOR ? "Erro no servidor" : "Erro na requisição")
  );
}

function errosNormalizados(errors: ProblemDetails["errors"]): Record<string, readonly string[]> {
  if (!errors) return {};
  return Object.fromEntries(
    Object.entries(errors).map(([campo, mensagens]) => [
      normalizarCaminhoDeCampo(campo),
      mensagens,
    ]),
  );
}

/** Monta o ErroApi a partir do corpo (ProblemDetails ou não) e do header X-Correlation-Id. */
export function erroDaResposta(
  status: number,
  corpo: unknown,
  correlationIdDoHeader: string | null,
): ErroApi {
  const problema: ProblemDetails = typeof corpo === "object" && corpo !== null ? corpo : {};
  const errosPorCampo = errosNormalizados(problema.errors);
  // Na validação o detail é genérico ("Um ou mais campos estão inválidos."): a 1ª mensagem de campo
  // diz o que fazer, e é ela que aparece quando o erro não cai num campo do formulário.
  const primeiraMensagem = Object.values(errosPorCampo)[0]?.[0];
  return new ErroApi({
    status,
    titulo: problema.title ?? tituloPadrao(status),
    detalhe: primeiraMensagem ?? problema.detail ?? undefined,
    codigo: problema.codigo ?? codigoDoTipo(problema.type ?? undefined),
    errosPorCampo,
    traceId: problema.traceId,
    correlationId: problema.correlationId ?? correlationIdDoHeader ?? undefined,
  });
}

export function erroDeRede(causa: unknown): ErroApi {
  return new ErroApi({
    status: 0,
    titulo: "Sem conexão com o servidor",
    detalhe: "Verifique a sua conexão e tente de novo.",
    causa,
  });
}

/** Mensagem curta para toast/estado de erro, sempre em pt-BR e dizendo o que fazer. */
export function mensagemDoErro(erro: unknown): { titulo: string; descricao?: string } {
  if (erro instanceof ErroApi) {
    return erro.detalhe && erro.detalhe !== erro.titulo
      ? { titulo: erro.titulo, descricao: erro.detalhe }
      : { titulo: erro.titulo };
  }
  return { titulo: "Algo deu errado", descricao: "Tente de novo em instantes." };
}

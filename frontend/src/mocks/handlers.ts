/**
 * Handlers MSW v2 de TODOS os endpoints do SPEC §7.1, com as regras de negócio, o escopo por
 * corretor (BOLA → 404), os papéis (403), ProblemDetails com correlationId, Idempotency-Key e os
 * efeitos assíncronos do worker (boas-vindas, triagem e notificações chegam depois).
 */
import { delay, http, HttpResponse, type HttpResponseResolver, type PathParams } from "msw";

import { ACOES_AUDITORIA, ENTIDADES_AUDITORIA } from "@/api/auditoria-codigos";
import type {
  AbrirSinistroRequest,
  AprovarSinistroRequest,
  AtualizarTaxaRequest,
  CancelamentoRequest,
  CotacaoRequest,
  EmitirApoliceRequest,
  EndossoRequest,
  LoginDemoRequest,
  LoginRequest,
  MotivoCancelamento,
  NegarSinistroRequest,
  Pagina,
  ProblemDetails,
  SimulacaoEndossoDto,
  StatusEfetivoApolice,
  StatusEfetivoCotacao,
  StatusSinistro,
} from "@/api/contratos";
import { cpfValido } from "@/ui/utils/documentos";

import {
  abrirSinistro,
  agendarNoWorker,
  ajustesDe,
  alcanca,
  apoliceDto,
  apoliceResumo,
  cotacaoDto,
  cotacaoResumo,
  criarCotacao,
  db,
  definirSessaoRefresh,
  emitirApolice,
  emitirToken,
  enviarBoasVindas,
  notificar,
  notificarSinistroAberto,
  produtoDoCodigo,
  registrarAuditoria,
  SENHA_MOCK,
  sinistroDto,
  sinistroResumo,
  statusApolice,
  statusCotacao,
  transicionar,
  triar,
  usuarioDto,
  usuarioPorId,
  type ApoliceMock,
  type SinistroMock,
  type UsuarioMock,
} from "./banco";
import { agoraIso, dataDoInstante, diasEntre, hoje, somarDias } from "./datas";
import { ajusteEndosso, ErroRegra, moeda, simularCancelamento, tarifar } from "./regras";

const api = (caminho: string) => `*/api/v1${caminho}`;
const MINUTOS_TOKEN = 15;
const LIMITE_PADRAO = 20;
const LIMITE_MAXIMO = 100;
const DESCRICAO_MINIMA = 20;
const DESCRICAO_MAXIMA = 2000;
const MOTIVO_MINIMO = 10;

// Mesmos títulos de src/Apolice.Api/Http/Problemas.cs.
const TITULOS: Record<number, string> = {
  400: "Requisição inválida",
  401: "Não autenticado",
  403: "Acesso negado",
  404: "Não encontrado",
  409: "Conflito",
  422: "Operação não permitida",
  429: "Muitas requisições",
  500: "Erro interno",
};

/* ---------------------------------------------------------------- respostas */

class Problema extends Error {
  readonly status: number;
  readonly codigo: string;
  readonly erros: Record<string, string[]> | undefined;

  constructor(status: number, codigo: string, mensagem: string, erros?: Record<string, string[]>) {
    super(mensagem);
    this.status = status;
    this.codigo = codigo;
    this.erros = erros;
  }
}

const naoEncontrado = (entidade: string) =>
  new Problema(
    404,
    "Comum.NaoEncontrado",
    `Nenhum registro de ${entidade} foi encontrado com o identificador informado.`,
  );
const proibido = () =>
  new Problema(403, "Comum.Proibido", "Seu perfil não tem permissão para esta operação.");
const desabilitada = () =>
  new Problema(
    404,
    "Comum.FuncionalidadeDesabilitada",
    "Esta funcionalidade está desabilitada no momento.",
  );
const validacao = (campo: string, mensagem: string) =>
  new Problema(400, "Comum.Validacao", mensagem, { [campo]: [mensagem] });

function responderProblema(p: Problema, correlationId: string) {
  const corpo: ProblemDetails = {
    type: `urn:apolice:problema:${p.codigo}`,
    title: TITULOS[p.status] ?? "Erro na requisição",
    status: p.status,
    detail: p.erros ? "Um ou mais campos estão inválidos." : p.message,
    codigo: p.codigo,
    traceId: `00-${crypto.randomUUID().replaceAll("-", "")}-01`,
    correlationId,
    ...(p.erros ? { errors: p.erros } : {}),
  };
  return HttpResponse.json(corpo, {
    status: p.status,
    headers: { "Content-Type": "application/problem+json", "X-Correlation-Id": correlationId },
  });
}

interface Contexto {
  request: Request;
  params: PathParams;
  usuario: UsuarioMock;
  correlationId: string;
}

type Resolvedor = (ctx: Contexto) => Response | Promise<Response>;
type ResolvedorAnonimo = (ctx: Omit<Contexto, "usuario">) => Response | Promise<Response>;

function correlationIdDe(request: Request): string {
  const recebido = request.headers.get("X-Correlation-Id");
  return recebido && /^[A-Za-z0-9-]{1,64}$/.test(recebido) ? recebido : crypto.randomUUID();
}

function autenticar(request: Request): UsuarioMock {
  const token = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  const registro = token ? db().tokens.get(token) : undefined;
  const usuario = registro?.valido ? usuarioPorId(registro.usuarioId) : undefined;
  if (!usuario) throw new Problema(401, "Auth.NaoAutenticado", "Autentique-se para continuar.");
  return usuario;
}

function falhaInjetada(request: Request): Response | null {
  const caminho = new URL(request.url).pathname;
  const indice = db().falhas.findIndex(
    (f) => f.metodo === request.method && f.caminho.test(caminho),
  );
  if (indice < 0) return null;
  const [falha] = db().falhas.splice(indice, 1);
  if (!falha) return null;
  return responderProblema(
    new Problema(falha.status, falha.codigo, falha.mensagem),
    falha.correlationId,
  );
}

function tratarErro(erro: unknown, correlationId: string): Response {
  if (erro instanceof Problema) return responderProblema(erro, correlationId);
  if (erro instanceof ErroRegra) {
    return responderProblema(
      new Problema(erro.tipo === "Validacao" ? 400 : 422, erro.codigo, erro.message),
      correlationId,
    );
  }
  throw erro;
}

function envolver(
  resolvedor: ResolvedorAnonimo,
  anonima: boolean,
  autenticado?: Resolvedor,
): HttpResponseResolver {
  return async ({ request, params }) => {
    const correlationId = correlationIdDe(request);
    if (db().config.latenciaMs > 0) await delay(db().config.latenciaMs);
    const falha = falhaInjetada(request);
    if (falha) return falha;
    try {
      if (anonima) return await resolvedor({ request, params, correlationId });
      const usuario = autenticar(request);
      return await (autenticado ?? resolvedor)({ request, params, usuario, correlationId });
    } catch (erro) {
      return tratarErro(erro, correlationId);
    }
  };
}

const rota = {
  get: (caminho: string, r: Resolvedor) =>
    http.get(api(caminho), envolver(r as ResolvedorAnonimo, false, r)),
  post: (caminho: string, r: Resolvedor) =>
    http.post(api(caminho), envolver(r as ResolvedorAnonimo, false, r)),
  put: (caminho: string, r: Resolvedor) =>
    http.put(api(caminho), envolver(r as ResolvedorAnonimo, false, r)),
  anonimaGet: (caminho: string, r: ResolvedorAnonimo) => http.get(api(caminho), envolver(r, true)),
  anonimaPost: (caminho: string, r: ResolvedorAnonimo) =>
    http.post(api(caminho), envolver(r, true)),
};

const json = (dado: unknown, status = 200) =>
  HttpResponse.json(dado as Record<string, unknown>, { status });

async function corpo<T>(request: Request): Promise<T> {
  try {
    return (await request.json()) as T;
  } catch {
    throw new Problema(400, "Comum.CorpoInvalido", "O corpo da requisição não é um JSON válido.");
  }
}

function param(ctx: { params: PathParams }, nome: string): string {
  const valor = ctx.params[nome];
  return typeof valor === "string" ? valor : "";
}

/* ---------------------------------------------------------------- auth */

function respostaAuth(usuario: UsuarioMock) {
  definirSessaoRefresh(usuario.id);
  return json({
    accessToken: emitirToken(usuario.id),
    expiraEm: new Date(Date.now() + MINUTOS_TOKEN * 60_000).toISOString(),
    usuario: usuarioDto(usuario),
  });
}

function exigirCsrf(request: Request) {
  if (request.headers.get("X-Requested-With") !== "apolice-web") {
    throw new Problema(403, "Auth.CabecalhoAusente", "Cabeçalho X-Requested-With ausente.");
  }
}

const handlersAuth = [
  rota.anonimaPost("/auth/login", async ({ request }) => {
    const { email, senha } = await corpo<LoginRequest>(request);
    const usuario = db().usuarios.find((u) => u.email === email.trim().toLowerCase());
    if (!usuario || senha !== SENHA_MOCK) {
      throw new Problema(401, "Auth.CredenciaisInvalidas", "E-mail ou senha inválidos.");
    }
    registrarAuditoria(
      usuario,
      ACOES_AUDITORIA.Login,
      ENTIDADES_AUDITORIA.Usuario,
      usuario.id,
      null,
      null,
    );
    return respostaAuth(usuario);
  }),
  rota.anonimaPost("/auth/demo", async ({ request }) => {
    if (!db().features.loginDemo) throw desabilitada();
    const { persona } = await corpo<LoginDemoRequest>(request);
    const usuario = db().usuarios.find((u) => u.persona === persona.trim().toLowerCase());
    if (!usuario) {
      throw validacao(
        "persona",
        "Persona de demonstração inválida (use corretora, corretor, analista ou admin).",
      );
    }
    registrarAuditoria(
      usuario,
      ACOES_AUDITORIA.LoginDemo,
      ENTIDADES_AUDITORIA.Usuario,
      usuario.id,
      { persona },
      null,
    );
    return respostaAuth(usuario);
  }),
  rota.anonimaPost("/auth/refresh", ({ request }) => {
    exigirCsrf(request);
    db().contadores.refresh += 1;
    const usuario = db().sessaoRefresh ? usuarioPorId(db().sessaoRefresh ?? "") : undefined;
    if (!usuario)
      throw new Problema(401, "Auth.RefreshInvalido", "Sua sessão expirou. Entre novamente.");
    return respostaAuth(usuario);
  }),
  rota.anonimaPost("/auth/logout", ({ request }) => {
    exigirCsrf(request);
    definirSessaoRefresh(null);
    return new HttpResponse(null, { status: 204 });
  }),
  rota.get("/auth/me", ({ usuario }) => {
    return json(usuarioDto(usuario));
  }),
  rota.anonimaGet("/features", () => json(db().features)),
];

/* ---------------------------------------------------------------- produtos */

const handlersProdutos = [
  rota.get("/produtos", () => json(db().produtos)),
  rota.get("/produtos/:codigo", (ctx) => {
    const produto = produtoDoCodigo(param(ctx, "codigo"));
    if (!produto) throw naoEncontrado("produto");
    return json(produto);
  }),
  rota.put("/produtos/:codigo/coberturas/:cobertura/taxa", async (ctx) => {
    if (ctx.usuario.papel !== "Admin") throw proibido();
    const produto = produtoDoCodigo(param(ctx, "codigo"));
    if (!produto) throw naoEncontrado("produto");
    const cobertura = produto.coberturas.find(
      (c) => c.codigo === param(ctx, "cobertura").toUpperCase(),
    );
    if (!cobertura) throw naoEncontrado("cobertura");
    const { taxa } = await corpo<AtualizarTaxaRequest>(ctx.request);
    if (!(taxa > 0 && taxa <= 1))
      throw validacao("taxa", "A taxa deve ser maior que zero e no máximo 1 (fração).");
    if (cobertura.tipoCalculo === "PremioFixo") {
      throw new Problema(
        422,
        "Produto.CoberturaPremioFixo",
        "Cobertura de prêmio fixo não aceita taxa.",
      );
    }
    const anterior = cobertura.taxa;
    cobertura.taxa = taxa;
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.TaxaAtualizada,
      ENTIDADES_AUDITORIA.Produto,
      produto.codigo,
      { cobertura: cobertura.codigo, taxaAnterior: anterior, taxaNova: taxa },
      ctx.correlationId,
    );
    return json(produto);
  }),
];

/* ---------------------------------------------------------------- paginação e busca */

function paginar<T>(itens: T[], request: Request): Pagina<T> {
  const url = new URL(request.url);
  const limiteTexto = url.searchParams.get("limite");
  const limite = limiteTexto ? Number(limiteTexto) : LIMITE_PADRAO;
  if (!Number.isInteger(limite) || limite < 1 || limite > LIMITE_MAXIMO) {
    throw validacao("limite", `O limite deve estar entre 1 e ${String(LIMITE_MAXIMO)}.`);
  }
  const cursor = url.searchParams.get("cursor");
  let inicio = 0;
  if (cursor) {
    inicio = Number(atob(cursor.replace(/-/g, "+").replace(/_/g, "/")));
    if (!Number.isInteger(inicio) || inicio < 0)
      throw validacao("cursor", "Cursor de paginação inválido.");
  }
  const fatia = itens.slice(inicio, inicio + limite);
  const proximo =
    inicio + limite < itens.length ? btoa(String(inicio + limite)).replace(/=+$/, "") : null;
  return { itens: fatia, proximoCursor: proximo };
}

const normalizar = (texto: string) =>
  texto
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();

function casaBusca(busca: string | null, ...campos: string[]): boolean {
  if (!busca?.trim()) return true;
  const termo = normalizar(busca.trim());
  const digitos = termo.replace(/\D/g, "");
  return campos.some(
    (c) =>
      normalizar(c).includes(termo) ||
      (digitos.length >= 3 && c.replace(/\D/g, "").includes(digitos)),
  );
}

const maisRecentePrimeiro =
  <T>(chave: (item: T) => string) =>
  (a: T, b: T) =>
    chave(b).localeCompare(chave(a));

/* ---------------------------------------------------------------- cotações */

function exigirCarteira(usuario: UsuarioMock) {
  if (usuario.papel !== "Corretor" && usuario.papel !== "Admin") throw proibido();
}

function validarPedidoCotacao(pedido: CotacaoRequest) {
  const erros: Record<string, string[]> = {};
  if (pedido.segurado.nome.trim().length < 3)
    erros["Segurado.Nome"] = ["Informe o nome do segurado."];
  if (!cpfValido(pedido.segurado.cpf)) erros["Segurado.Cpf"] = ["CPF inválido."];
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(pedido.segurado.email))
    erros["Segurado.Email"] = ["E-mail inválido."];
  if (!pedido.segurado.dataNascimento)
    erros["Segurado.DataNascimento"] = ["Informe a data de nascimento."];
  if (Object.keys(erros).length > 0)
    throw new Problema(400, "Comum.Validacao", "Um ou mais campos são inválidos.", erros);
}

function tarifarPedido(pedido: CotacaoRequest) {
  validarPedidoCotacao(pedido);
  const produto = produtoDoCodigo(pedido.produtoCodigo);
  if (!produto) throw validacao("ProdutoCodigo", "Produto inválido (use AUTO, RESID ou VIDA).");
  const segurado = {
    ...pedido.segurado,
    cpf: formatarCpf(pedido.segurado.cpf),
    email: pedido.segurado.email.trim().toLowerCase(),
  };
  return {
    produto,
    segurado,
    tarifacao: tarifar(produto, segurado, pedido.dadosRisco, pedido.coberturas, hoje()),
  };
}

function formatarCpf(cpf: string): string {
  const d = cpf.replace(/\D/g, "");
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

function cotacaoDoUsuario(ctx: Contexto) {
  const cotacao = db().cotacoes.find((c) => c.id === param(ctx, "id"));
  if (!cotacao || !alcanca(ctx.usuario, cotacao.corretorId)) throw naoEncontrado("cotação");
  return cotacao;
}

const handlersCotacoes = [
  rota.post("/cotacoes/simular", async ({ request, usuario }) => {
    exigirCarteira(usuario);
    return json(tarifarPedido(await corpo<CotacaoRequest>(request)).tarifacao);
  }),
  rota.post("/cotacoes", async ({ request, usuario, correlationId }) => {
    exigirCarteira(usuario);
    const pedido = await corpo<CotacaoRequest>(request);
    const { produto, segurado, tarifacao } = tarifarPedido(pedido);
    const cotacao = criarCotacao(usuario.id, produto, segurado, pedido.dadosRisco, tarifacao);
    registrarAuditoria(
      usuario,
      ACOES_AUDITORIA.CotacaoCriada,
      ENTIDADES_AUDITORIA.Cotacao,
      cotacao.id,
      { produto: produto.codigo, premioTotal: tarifacao.premioTotal },
      correlationId,
    );
    return json(cotacaoDto(cotacao), 201);
  }),
  rota.get("/cotacoes", ({ request, usuario }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get("status") as StatusEfetivoCotacao | null;
    const produto = url.searchParams.get("produto")?.toUpperCase();
    const busca = url.searchParams.get("busca");
    const itens = db()
      .cotacoes.filter((c) => alcanca(usuario, c.corretorId))
      .filter((c) => !status || statusCotacao(c) === status)
      .filter((c) => !produto || c.produtoCodigo === produto)
      .filter((c) => casaBusca(busca, c.segurado.nome, c.segurado.cpf, c.id))
      .sort(maisRecentePrimeiro((c) => new Date(c.criadaEm).toISOString()))
      .map(cotacaoResumo);
    return json(paginar(itens, request));
  }),
  rota.get("/cotacoes/:id", (ctx) => json(cotacaoDto(cotacaoDoUsuario(ctx)))),
  rota.post("/cotacoes/:id/aceitar", (ctx) => {
    const cotacao = cotacaoDoUsuario(ctx);
    exigirCarteira(ctx.usuario);
    const status = statusCotacao(cotacao);
    if (status === "Expirada")
      throw new Problema(422, "Cotacao.Expirada", "A cotação expirou. Faça uma nova cotação.");
    if (status !== "Calculada")
      throw new Problema(
        422,
        "Cotacao.StatusInvalido",
        "Só uma cotação calculada pode ser aceita.",
      );
    cotacao.status = "Aceita";
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.CotacaoAceita,
      ENTIDADES_AUDITORIA.Cotacao,
      cotacao.id,
      null,
      ctx.correlationId,
    );
    return json(cotacaoDto(cotacao));
  }),
  rota.post("/cotacoes/:id/recusar", (ctx) => {
    const cotacao = cotacaoDoUsuario(ctx);
    exigirCarteira(ctx.usuario);
    if (cotacao.status !== "Calculada" && cotacao.status !== "Aceita") {
      throw new Problema(
        422,
        "Cotacao.StatusInvalido",
        "Só uma cotação calculada ou aceita pode ser recusada.",
      );
    }
    cotacao.status = "Recusada";
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.CotacaoRecusada,
      ENTIDADES_AUDITORIA.Cotacao,
      cotacao.id,
      null,
      ctx.correlationId,
    );
    return json(cotacaoDto(cotacao));
  }),
];

/* ---------------------------------------------------------------- idempotência */

async function comIdempotencia(
  ctx: Contexto,
  executar: () => Promise<Response> | Response,
): Promise<Response> {
  const chave = ctx.request.headers.get("Idempotency-Key");
  if (!chave) return executar();
  if (chave.length > 100)
    throw validacao(
      "Idempotency-Key",
      "A chave de idempotência deve ter no máximo 100 caracteres.",
    );
  const texto = await ctx.request.clone().text();
  const registro = db().idempotencia.get(`${ctx.usuario.id}:${chave}`);
  if (registro) {
    if (registro.corpo !== texto) {
      throw new Problema(
        422,
        "Idempotencia.CorpoDiferente",
        "A mesma chave de idempotência foi usada com outro conteúdo.",
      );
    }
    return json(registro.resposta, 201);
  }
  const resposta = await executar();
  if (resposta.ok) {
    db().idempotencia.set(`${ctx.usuario.id}:${chave}`, {
      corpo: texto,
      resposta: await resposta.clone().json(),
    });
  }
  return resposta;
}

/* ---------------------------------------------------------------- apólices */

function apoliceDoUsuario(ctx: Contexto): ApoliceMock {
  const apolice = db().apolices.find((a) => a.id === param(ctx, "id"));
  if (!apolice || !alcanca(ctx.usuario, apolice.corretorId)) throw naoEncontrado("apólice");
  return apolice;
}

function exigirOperavel(apolice: ApoliceMock) {
  const status = statusApolice(apolice);
  if (status !== "Ativa" && status !== "Programada") {
    throw new Problema(
      422,
      "Apolice.StatusInvalido",
      `A apólice está ${status.toLowerCase()} e não aceita esta operação.`,
    );
  }
}

function simularEndossoDe(apolice: ApoliceMock, pedido: EndossoRequest) {
  exigirOperavel(apolice);
  const produto = produtoDoCodigo(apolice.produtoCodigo);
  if (!produto) throw naoEncontrado("produto");
  const codigo = pedido.coberturaCodigo.trim().toUpperCase();
  const atual = apolice.coberturas.find((c) => c.codigo === codigo);
  const catalogo = produto.coberturas.find((c) => c.codigo === codigo);
  if (!catalogo)
    throw new Problema(
      422,
      "Endosso.CoberturaInexistente",
      `A cobertura '${codigo}' não existe no produto.`,
    );
  if (pedido.tipo === "InclusaoCobertura" && atual)
    throw new Problema(
      422,
      "Endosso.CoberturaJaContratada",
      "A cobertura já faz parte da apólice.",
    );
  if (pedido.tipo !== "InclusaoCobertura" && !atual)
    throw new Problema(
      422,
      "Endosso.CoberturaNaoContratada",
      "A cobertura não faz parte da apólice.",
    );
  if (pedido.tipo === "ExclusaoCobertura" && catalogo.obrigatoria)
    throw new Problema(
      422,
      "Endosso.CoberturaObrigatoria",
      "Cobertura obrigatória não pode ser excluída.",
    );
  if (pedido.tipo === "AlteracaoLmi" && pedido.lmi === null)
    throw validacao("Lmi", "Informe o novo LMI.");

  const base = apolice.coberturas
    .filter((c) => c.codigo !== codigo)
    .map((c) => ({ codigo: c.codigo, lmi: c.lmi }));
  const conjunto =
    pedido.tipo === "ExclusaoCobertura" ? base : [...base, { codigo, lmi: pedido.lmi }];
  const t = tarifar(
    produto,
    apolice.segurado,
    apolice.dadosRisco,
    conjunto,
    dataDoInstante(apolice.emitidaEm),
  );
  const novoItem = t.itens.find((i) => i.codigo === codigo) ?? null;
  const dataEfeito = hoje() > apolice.vigenciaInicio ? hoje() : apolice.vigenciaInicio;
  const diasVigencia = diasEntre(apolice.vigenciaInicio, apolice.vigenciaFim);
  const diasRestantes = diasEntre(dataEfeito, apolice.vigenciaFim);
  const anterior = atual?.premioLiquido ?? 0;
  const novo = novoItem?.premioLiquido ?? 0;
  const simulacao: SimulacaoEndossoDto = {
    dataEfeito,
    diasRestantes,
    diasVigencia,
    premioAnualAnterior: anterior,
    premioAnualNovo: novo,
    ...ajusteEndosso(anterior, novo, diasRestantes, diasVigencia, apolice.aliquotaIof),
  };
  return { simulacao, novoItem, atual, codigo };
}

const ROTULO_TIPO_ENDOSSO = {
  AlteracaoLmi: "alteração de LMI",
  InclusaoCobertura: "inclusão",
  ExclusaoCobertura: "exclusão",
} as const;
const ROTULO_MOTIVO: Record<MotivoCancelamento, string> = {
  PedidoDoSegurado: "pedido do segurado",
  IniciativaSeguradora: "iniciativa da seguradora",
  Inadimplencia: "inadimplência",
};

const handlersApolices = [
  rota.post("/apolices", (ctx) =>
    comIdempotencia(ctx, async () => {
      const pedido = await corpo<EmitirApoliceRequest>(ctx.request.clone());
      const cotacao = db().cotacoes.find((c) => c.id === pedido.cotacaoId);
      if (!cotacao || !alcanca(ctx.usuario, cotacao.corretorId)) throw naoEncontrado("cotação");
      exigirCarteira(ctx.usuario);
      const status = statusCotacao(cotacao);
      if (status === "Expirada")
        throw new Problema(422, "Cotacao.Expirada", "A cotação expirou. Faça uma nova cotação.");
      if (status !== "Aceita")
        throw new Problema(
          422,
          "Cotacao.StatusInvalido",
          "Só uma cotação aceita pode ser emitida.",
        );
      const limite = somarDias(hoje(), 30);
      if (pedido.inicioVigencia < hoje() || pedido.inicioVigencia > limite) {
        throw new Problema(
          400,
          "Apolice.InicioVigenciaInvalido",
          "O início da vigência deve estar entre hoje e 30 dias.",
          {
            InicioVigencia: ["O início da vigência deve estar entre hoje e 30 dias."],
          },
        );
      }
      if (pedido.parcelas < 1 || pedido.parcelas > cotacao.tarifacao.maxParcelas) {
        throw new Problema(
          400,
          "Apolice.ParcelasInvalidas",
          `Escolha de 1 a ${String(cotacao.tarifacao.maxParcelas)} parcelas.`,
          {
            Parcelas: [`Escolha de 1 a ${String(cotacao.tarifacao.maxParcelas)} parcelas.`],
          },
        );
      }
      const apolice = emitirApolice(cotacao, pedido.inicioVigencia, pedido.parcelas);
      registrarAuditoria(
        ctx.usuario,
        ACOES_AUDITORIA.ApoliceEmitida,
        ENTIDADES_AUDITORIA.Apolice,
        apolice.id,
        { numero: apolice.numero },
        ctx.correlationId,
      );
      agendarNoWorker(() => enviarBoasVindas(apolice));
      return json(apoliceDto(apolice), 201);
    }),
  ),
  rota.get("/apolices", ({ request, usuario }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get("status") as StatusEfetivoApolice | null;
    const produto = url.searchParams.get("produto")?.toUpperCase();
    const busca = url.searchParams.get("busca");
    const itens = db()
      .apolices.filter((a) => alcanca(usuario, a.corretorId))
      .filter((a) => !status || statusApolice(a) === status)
      .filter((a) => !produto || a.produtoCodigo === produto)
      .filter((a) => casaBusca(busca, a.numero, a.segurado.nome, a.segurado.cpf))
      .sort(maisRecentePrimeiro((a) => new Date(a.emitidaEm).toISOString()))
      .map(apoliceResumo);
    return json(paginar(itens, request));
  }),
  rota.get("/apolices/:id", (ctx) => json(apoliceDto(apoliceDoUsuario(ctx)))),
  rota.get("/apolices/:id/documento", (ctx) => {
    const apolice = apoliceDoUsuario(ctx);
    if (!db().features.documentoPdf) throw desabilitada();
    return new HttpResponse(pdfMinimo(apolice.numero), {
      headers: {
        "Content-Type": "application/pdf",
        "Content-Disposition": `attachment; filename="${apolice.numero}.pdf"`,
      },
    });
  }),
  rota.post("/apolices/:id/endossos/simular", async (ctx) => {
    const apolice = apoliceDoUsuario(ctx);
    if (!db().features.endossoOnline) throw desabilitada();
    exigirCarteira(ctx.usuario);
    return json(simularEndossoDe(apolice, await corpo<EndossoRequest>(ctx.request)).simulacao);
  }),
  rota.post("/apolices/:id/endossos", async (ctx) => {
    const apolice = apoliceDoUsuario(ctx);
    if (!db().features.endossoOnline) throw desabilitada();
    exigirCarteira(ctx.usuario);
    const pedido = await corpo<EndossoRequest>(ctx.request);
    const { simulacao, novoItem, atual, codigo } = simularEndossoDe(apolice, pedido);
    const numero = apolice.endossos.length + 1;
    apolice.endossos.push({
      id: crypto.randomUUID(),
      numero,
      tipo: pedido.tipo,
      coberturaCodigo: codigo,
      coberturaNome: novoItem?.nome ?? atual?.nome ?? codigo,
      lmiAnterior: atual?.lmi ?? null,
      lmiNovo: novoItem?.lmi ?? null,
      dataEfeito: simulacao.dataEfeito,
      ajusteLiquido: simulacao.ajusteLiquido,
      iofAjuste: simulacao.iofAjuste,
      ajusteTotal: simulacao.ajusteTotal,
      criadoEm: agoraIso(),
      criadoPorNome: ctx.usuario.nome,
    });
    apolice.coberturas =
      pedido.tipo === "ExclusaoCobertura"
        ? apolice.coberturas.filter((c) => c.codigo !== codigo)
        : atual
          ? apolice.coberturas.map((c) => (c.codigo === codigo && novoItem ? { ...novoItem } : c))
          : [...apolice.coberturas, ...(novoItem ? [{ ...novoItem }] : [])];
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.ApoliceEndossada,
      ENTIDADES_AUDITORIA.Apolice,
      apolice.id,
      { numero: apolice.numero, endosso: numero, tipo: pedido.tipo },
      ctx.correlationId,
    );
    agendarNoWorker(() =>
      notificar(
        apolice.id,
        "Endosso",
        apolice.segurado.email,
        `Endosso nº ${String(numero)} da apólice ${apolice.numero}`,
        `Olá, ${apolice.segurado.nome}.\n\nRegistramos o endosso nº ${String(numero)} na sua apólice ${apolice.numero}: ${ROTULO_TIPO_ENDOSSO[pedido.tipo]} da cobertura ${novoItem?.nome ?? atual?.nome ?? codigo}. Ajuste: ${moeda(simulacao.ajusteTotal)}.`,
      ),
    );
    return json(apoliceDto(apolice), 201);
  }),
  rota.post("/apolices/:id/cancelamento/simular", async (ctx) => {
    const apolice = apoliceDoUsuario(ctx);
    exigirCarteira(ctx.usuario);
    exigirOperavel(apolice);
    const { motivo } = await corpo<CancelamentoRequest>(ctx.request);
    return json(
      simularCancelamento(
        apolice.premioLiquido + ajustesDe(apolice),
        apolice.vigenciaInicio,
        apolice.vigenciaFim,
        motivo,
        hoje(),
      ),
    );
  }),
  rota.post("/apolices/:id/cancelamento", async (ctx) => {
    const apolice = apoliceDoUsuario(ctx);
    exigirCarteira(ctx.usuario);
    exigirOperavel(apolice);
    const { motivo } = await corpo<CancelamentoRequest>(ctx.request);
    const simulacao = simularCancelamento(
      apolice.premioLiquido + ajustesDe(apolice),
      apolice.vigenciaInicio,
      apolice.vigenciaFim,
      motivo,
      hoje(),
    );
    apolice.status = "Cancelada";
    apolice.canceladaEm = agoraIso();
    apolice.motivoCancelamento = motivo;
    apolice.valorRestituicao = simulacao.valorRestituicao;
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.ApoliceCancelada,
      ENTIDADES_AUDITORIA.Apolice,
      apolice.id,
      { numero: apolice.numero, motivo },
      ctx.correlationId,
    );
    agendarNoWorker(() =>
      notificar(
        apolice.id,
        "Cancelamento",
        apolice.segurado.email,
        `Cancelamento da apólice ${apolice.numero}`,
        `Olá, ${apolice.segurado.nome}.\n\nConfirmamos o cancelamento da sua apólice ${apolice.numero}, por ${ROTULO_MOTIVO[motivo]}.\nValor a restituir: ${moeda(simulacao.valorRestituicao)} (o IOF não é restituível).`,
      ),
    );
    return json(apoliceDto(apolice));
  }),
];

/** PDF de 1 página válido (o backend usa QuestPDF; aqui basta o arquivo abrir). */
function pdfMinimo(numero: string): Uint8Array {
  const texto = `Apolice ${numero} - documento de demonstracao`;
  const conteudo = `BT /F1 18 Tf 60 760 Td (${texto}) Tj ET`;
  const objetos = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    `<< /Length ${String(conteudo.length)} >>\nstream\n${conteudo}\nendstream`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
  ];
  let pdf = "%PDF-1.4\n";
  const posicoes: number[] = [];
  objetos.forEach((o, i) => {
    posicoes.push(pdf.length);
    pdf += `${String(i + 1)} 0 obj\n${o}\nendobj\n`;
  });
  const xref = pdf.length;
  pdf += `xref\n0 ${String(objetos.length + 1)}\n0000000000 65535 f \n`;
  pdf += posicoes.map((p) => `${String(p).padStart(10, "0")} 00000 n \n`).join("");
  pdf += `trailer\n<< /Size ${String(objetos.length + 1)} /Root 1 0 R >>\nstartxref\n${String(xref)}\n%%EOF`;
  return new TextEncoder().encode(pdf);
}

/* ---------------------------------------------------------------- sinistros */

function sinistroDoUsuario(ctx: Contexto): SinistroMock {
  const sinistro = db().sinistros.find((s) => s.id === param(ctx, "id"));
  const apolice = sinistro ? db().apolices.find((a) => a.id === sinistro.apoliceId) : undefined;
  if (!sinistro || !apolice || !alcanca(ctx.usuario, apolice.corretorId))
    throw naoEncontrado("sinistro");
  return sinistro;
}

function exigirAnalista(usuario: UsuarioMock) {
  if (usuario.papel !== "Analista" && usuario.papel !== "Admin") throw proibido();
}

const transicaoInvalida = (status: StatusSinistro) =>
  new Problema(
    422,
    "Sinistro.TransicaoInvalida",
    `Transição inválida para um sinistro com status ${status}.`,
  );

function validarAbertura(apolice: ApoliceMock, pedido: AbrirSinistroRequest) {
  const cobertura = apolice.coberturas.find(
    (c) => c.codigo === pedido.coberturaCodigo.toUpperCase(),
  );
  if (!cobertura)
    throw new Problema(
      422,
      "Sinistro.CoberturaInexistente",
      "A cobertura informada não faz parte da apólice.",
    );
  if (!cobertura.permiteSinistro)
    throw new Problema(
      422,
      "Sinistro.CoberturaSemSinistro",
      `A cobertura ${cobertura.nome} não permite abertura de sinistro.`,
    );
  if (pedido.dataOcorrencia > hoje())
    throw validacao("DataOcorrencia", "A data da ocorrência não pode ser futura.");
  if (
    pedido.dataOcorrencia < apolice.vigenciaInicio ||
    pedido.dataOcorrencia > apolice.vigenciaFim
  ) {
    throw new Problema(
      422,
      "Sinistro.ForaDaVigencia",
      "A ocorrência precisa estar dentro da vigência da apólice.",
    );
  }
  if (apolice.canceladaEm && pedido.dataOcorrencia >= dataDoInstante(apolice.canceladaEm)) {
    throw new Problema(
      422,
      "Sinistro.AposCancelamento",
      "A ocorrência é posterior ao cancelamento da apólice.",
    );
  }
  const tamanho = pedido.descricao.trim().length;
  if (tamanho < DESCRICAO_MINIMA || tamanho > DESCRICAO_MAXIMA) {
    throw validacao(
      "Descricao",
      `Descreva a ocorrência com ${String(DESCRICAO_MINIMA)} a ${String(DESCRICAO_MAXIMA)} caracteres.`,
    );
  }
  if (!(pedido.valorReclamado > 0))
    throw validacao("ValorReclamado", "O valor reclamado deve ser maior que zero.");
  return cobertura;
}

const handlersSinistros = [
  rota.post("/sinistros", (ctx) =>
    comIdempotencia(ctx, async () => {
      const pedido = await corpo<AbrirSinistroRequest>(ctx.request.clone());
      const apolice = db().apolices.find((a) => a.id === pedido.apoliceId);
      if (!apolice || !alcanca(ctx.usuario, apolice.corretorId)) throw naoEncontrado("apólice");
      exigirCarteira(ctx.usuario);
      const cobertura = validarAbertura(apolice, pedido);
      const sinistro = abrirSinistro(
        apolice,
        cobertura.codigo,
        pedido.dataOcorrencia,
        pedido.descricao.trim(),
        pedido.valorReclamado,
        ctx.usuario.id,
      );
      registrarAuditoria(
        ctx.usuario,
        ACOES_AUDITORIA.SinistroAberto,
        ENTIDADES_AUDITORIA.Sinistro,
        sinistro.id,
        { numero: sinistro.numero, apolice: apolice.numero },
        ctx.correlationId,
      );
      agendarNoWorker(() => {
        if (db().features.triagemAutomaticaSinistros) triar(sinistro);
        notificarSinistroAberto(sinistro);
      });
      return json(sinistroDto(sinistro), 201);
    }),
  ),
  rota.get("/sinistros", ({ request, usuario }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get("status") as StatusSinistro | null;
    const busca = url.searchParams.get("busca");
    const itens = db()
      .sinistros.filter((s) => {
        const apolice = db().apolices.find((a) => a.id === s.apoliceId);
        return apolice !== undefined && alcanca(usuario, apolice.corretorId);
      })
      .filter((s) => !status || s.status === status)
      .map(sinistroResumo)
      .filter((s) => casaBusca(busca, s.numero, s.apoliceNumero, s.seguradoNome))
      .sort(maisRecentePrimeiro((s) => new Date(s.abertoEm).toISOString()));
    return json(paginar(itens, request));
  }),
  rota.get("/sinistros/:id", (ctx) => json(sinistroDto(sinistroDoUsuario(ctx)))),
  rota.post("/sinistros/:id/analise", (ctx) => {
    const sinistro = sinistroDoUsuario(ctx);
    exigirAnalista(ctx.usuario);
    if (sinistro.status !== "Aberto") throw transicaoInvalida(sinistro.status);
    transicionar(sinistro, "EmAnalise", ctx.usuario.nome, null);
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.SinistroEmAnalise,
      ENTIDADES_AUDITORIA.Sinistro,
      sinistro.id,
      { numero: sinistro.numero },
      ctx.correlationId,
    );
    return json(sinistroDto(sinistro));
  }),
  rota.post("/sinistros/:id/aprovacao", async (ctx) => {
    const sinistro = sinistroDoUsuario(ctx);
    exigirAnalista(ctx.usuario);
    if (sinistro.status !== "EmAnalise") throw transicaoInvalida(sinistro.status);
    const { valorIndenizacao, observacao } = await corpo<AprovarSinistroRequest>(ctx.request);
    const teto = Math.min(sinistro.valorReclamado, sinistro.lmiCobertura);
    if (!(valorIndenizacao > 0) || valorIndenizacao > teto) {
      throw new Problema(
        400,
        "Sinistro.ValorIndenizacaoInvalido",
        `A indenização deve ser maior que zero e no máximo ${moeda(teto)}.`,
        {
          ValorIndenizacao: [`A indenização deve ser maior que zero e no máximo ${moeda(teto)}.`],
        },
      );
    }
    sinistro.valorIndenizacao = valorIndenizacao;
    transicionar(
      sinistro,
      "Aprovado",
      ctx.usuario.nome,
      observacao?.trim() ? observacao.trim() : null,
    );
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.SinistroAprovado,
      ENTIDADES_AUDITORIA.Sinistro,
      sinistro.id,
      { numero: sinistro.numero, valorIndenizacao },
      ctx.correlationId,
    );
    agendarNoWorker(() =>
      notificarDecisao(
        sinistro,
        `Sinistro ${sinistro.numero} aprovado`,
        `Temos uma boa notícia: o sinistro ${sinistro.numero} foi aprovado.\nValor da indenização: ${moeda(valorIndenizacao)}.`,
      ),
    );
    return json(sinistroDto(sinistro));
  }),
  rota.post("/sinistros/:id/negativa", async (ctx) => {
    const sinistro = sinistroDoUsuario(ctx);
    exigirAnalista(ctx.usuario);
    if (sinistro.status !== "EmAnalise") throw transicaoInvalida(sinistro.status);
    const { motivo } = await corpo<NegarSinistroRequest>(ctx.request);
    if (motivo.trim().length < MOTIVO_MINIMO) {
      throw validacao(
        "Motivo",
        `Explique o motivo da negativa com pelo menos ${String(MOTIVO_MINIMO)} caracteres.`,
      );
    }
    sinistro.motivoNegativa = motivo.trim();
    transicionar(sinistro, "Negado", ctx.usuario.nome, motivo.trim());
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.SinistroNegado,
      ENTIDADES_AUDITORIA.Sinistro,
      sinistro.id,
      { numero: sinistro.numero },
      ctx.correlationId,
    );
    agendarNoWorker(() =>
      notificarDecisao(
        sinistro,
        `Sinistro ${sinistro.numero}: resultado da análise`,
        `Após análise cuidadosa, o sinistro ${sinistro.numero} não pôde ser aprovado.\nMotivo: ${motivo.trim()}`,
      ),
    );
    return json(sinistroDto(sinistro));
  }),
  rota.post("/sinistros/:id/pagamento", (ctx) => {
    const sinistro = sinistroDoUsuario(ctx);
    exigirAnalista(ctx.usuario);
    if (sinistro.status !== "Aprovado") throw transicaoInvalida(sinistro.status);
    transicionar(sinistro, "Pago", ctx.usuario.nome, null);
    registrarAuditoria(
      ctx.usuario,
      ACOES_AUDITORIA.SinistroPago,
      ENTIDADES_AUDITORIA.Sinistro,
      sinistro.id,
      { numero: sinistro.numero },
      ctx.correlationId,
    );
    agendarNoWorker(() =>
      notificarDecisao(
        sinistro,
        `Pagamento do sinistro ${sinistro.numero}`,
        `Informamos que o pagamento da indenização do sinistro ${sinistro.numero} foi realizado.\nValor pago: ${moeda(sinistro.valorIndenizacao ?? 0)}.`,
      ),
    );
    return json(sinistroDto(sinistro));
  }),
];

function notificarDecisao(sinistro: SinistroMock, assunto: string, texto: string) {
  const apolice = db().apolices.find((a) => a.id === sinistro.apoliceId);
  if (!apolice) return;
  notificar(
    sinistro.id,
    "SinistroDecidido",
    apolice.segurado.email,
    assunto,
    `Olá, ${apolice.segurado.nome}.\n\n${texto}`,
  );
}

/* ---------------------------------------------------------------- dashboard e auditoria */

function mesDe(data: string): string {
  return data.slice(0, 7);
}

function ultimosMeses(quantidade: number): string[] {
  const [ano, mes] = hoje().split("-").map(Number) as [number, number];
  return Array.from({ length: quantidade }, (_, i) => {
    const d = new Date(Date.UTC(ano, mes - 1 - (quantidade - 1 - i), 1));
    return d.toISOString().slice(0, 7);
  });
}

const handlersPainel = [
  rota.get("/dashboard", ({ usuario }) => {
    const dentro = (corretorId: string) => alcanca(usuario, corretorId);
    const apolices = db().apolices.filter((a) => dentro(a.corretorId));
    const cotacoes = db().cotacoes.filter((c) => dentro(c.corretorId));
    const idsApolices = new Set(apolices.map((a) => a.id));
    const sinistros = db().sinistros.filter((s) => idsApolices.has(s.apoliceId));
    const meses = ultimosMeses(12);
    const premioDoMes = (mes: string) =>
      Math.round(
        apolices
          .filter((a) => mesDe(dataDoInstante(a.emitidaEm)) === mes)
          .reduce((s, a) => s + a.premioTotal, 0) * 100,
      ) / 100;
    const premio12 = meses.map((mes) => ({ mes, valor: premioDoMes(mes) }));
    const total12 = premio12.reduce((s, p) => s + p.valor, 0);
    const corte90 = Date.now() - 90 * 86_400_000;
    const criadas90 = cotacoes.filter((c) => new Date(c.criadaEm).getTime() >= corte90);
    const emitidas90 = criadas90.filter((c) => c.status === "Emitida").length;
    const indenizado = sinistros
      .filter(
        (s) =>
          (s.status === "Aprovado" || s.status === "Pago") &&
          meses.includes(mesDe(dataDoInstante(s.abertoEm))),
      )
      .reduce((s, x) => s + (x.valorIndenizacao ?? 0), 0);
    const ativas = apolices.filter((a) => statusApolice(a) === "Ativa");
    return json({
      escopo: usuario.papel === "Corretor" ? "Carteira" : "Global",
      apolicesAtivas: ativas.length,
      premioEmitidoMes: premioDoMes(mesDe(hoje())),
      premioEmitido12Meses: premio12,
      cotacoesAbertas: cotacoes.filter((c) => ["Calculada", "Aceita"].includes(statusCotacao(c)))
        .length,
      taxaConversao90d:
        criadas90.length === 0 ? 0 : Math.round((emitidas90 / criadas90.length) * 10_000) / 10_000,
      sinistrosEmAberto: sinistros.filter((s) => s.status === "Aberto" || s.status === "EmAnalise")
        .length,
      sinistralidade12m: total12 === 0 ? 0 : Math.round((indenizado / total12) * 10_000) / 10_000,
      porProduto: db().produtos.map((p) => ({
        produtoCodigo: p.codigo,
        produtoNome: p.nome,
        apolicesAtivas: ativas.filter((a) => a.produtoCodigo === p.codigo).length,
        premioEmitido:
          Math.round(
            apolices
              .filter(
                (a) =>
                  a.produtoCodigo === p.codigo &&
                  meses.includes(mesDe(dataDoInstante(a.emitidaEm))),
              )
              .reduce((s, a) => s + a.premioTotal, 0) * 100,
          ) / 100,
      })),
    });
  }),
  rota.get("/admin/auditoria", ({ request, usuario }) => {
    if (usuario.papel !== "Admin") throw proibido();
    const itens = [...db().auditoria].sort(
      maisRecentePrimeiro((a) => new Date(a.ocorridoEm).toISOString()),
    );
    return json(paginar(itens, request));
  }),
];

export const handlers = [
  ...handlersAuth,
  ...handlersProdutos,
  ...handlersCotacoes,
  ...handlersApolices,
  ...handlersSinistros,
  ...handlersPainel,
];

/**
 * Banco em memória do mock (MSW): estado coerente com as regras do SPEC, recriado a cada teste
 * (resetarBanco) e, no modo dev (VITE_API_MOCK=1), a cada recarga da página.
 */
import { ACOES_AUDITORIA, ENTIDADES_AUDITORIA } from "@/api/auditoria-codigos";
import type {
  ApoliceDto,
  ApoliceResumoDto,
  AuditoriaDto,
  CodigoProduto,
  CotacaoDto,
  CotacaoResumoDto,
  DadosRisco,
  EndossoDto,
  FeaturesDto,
  HistoricoSinistroDto,
  MotivoCancelamento,
  NotificacaoDto,
  Papel,
  ProdutoDto,
  SeguradoDto,
  SinistroDto,
  SinistroResumoDto,
  StatusEfetivoApolice,
  StatusEfetivoCotacao,
  StatusSinistro,
  TarifacaoDto,
  TipoNotificacao,
  TriagemDto,
  UsuarioDto,
} from "@/api/contratos";
import { mascararCpf } from "@/ui/utils/formatar";

import { criarCatalogo } from "./catalogo";
import {
  agoraIso,
  anoDe,
  dataDoInstante,
  diasEntre,
  hoje,
  instante,
  somarDias,
  somarMeses,
} from "./datas";
import { avaliarTriagem, cronograma, moeda, simularCancelamento, tarifar } from "./regras";

/* ---------------------------------------------------------------- tipos internos */

export interface UsuarioMock extends UsuarioDto {
  persona: string;
}

export interface CotacaoMock {
  id: string;
  produtoCodigo: CodigoProduto;
  segurado: SeguradoDto;
  dadosRisco: DadosRisco;
  tarifacao: TarifacaoDto;
  status: "Calculada" | "Aceita" | "Emitida" | "Recusada";
  validaAte: string;
  criadaEm: string;
  corretorId: string;
  apoliceId: string | null;
}

export interface ApoliceMock {
  id: string;
  numero: string;
  cotacaoId: string;
  produtoCodigo: CodigoProduto;
  segurado: SeguradoDto;
  dadosRisco: DadosRisco;
  vigenciaInicio: string;
  vigenciaFim: string;
  coberturas: ApoliceDto["coberturas"];
  premioLiquido: number;
  aliquotaIof: number;
  iof: number;
  premioTotal: number;
  parcelas: number;
  status: "Ativa" | "Cancelada";
  emitidaEm: string;
  canceladaEm: string | null;
  motivoCancelamento: MotivoCancelamento | null;
  valorRestituicao: number | null;
  boasVindasEnviadasEm: string | null;
  corretorId: string;
  endossos: EndossoDto[];
}

export interface SinistroMock {
  id: string;
  numero: string;
  apoliceId: string;
  coberturaCodigo: string;
  coberturaNome: string;
  lmiCobertura: number;
  dataOcorrencia: string;
  descricao: string;
  valorReclamado: number;
  valorIndenizacao: number | null;
  status: StatusSinistro;
  motivoNegativa: string | null;
  triagem: TriagemDto | null;
  abertoEm: string;
  historico: HistoricoSinistroDto[];
}

export interface NotificacaoMock extends NotificacaoDto {
  referenciaId: string;
}

export interface ConfigMock {
  /** Atraso dos efeitos do worker (boas-vindas, triagem, notificações). */
  atrasoWorkerMs: number;
  /** Latência simulada das respostas (0 nos testes). */
  latenciaMs: number;
}

export interface FalhaInjetada {
  metodo: string;
  caminho: RegExp;
  status: number;
  codigo: string;
  mensagem: string;
  correlationId: string;
}

interface Banco {
  produtos: ProdutoDto[];
  usuarios: UsuarioMock[];
  cotacoes: CotacaoMock[];
  apolices: ApoliceMock[];
  sinistros: SinistroMock[];
  notificacoes: NotificacaoMock[];
  auditoria: AuditoriaDto[];
  numeradores: Map<string, number>;
  idempotencia: Map<string, { corpo: string; resposta: unknown }>;
  features: FeaturesDto;
  config: ConfigMock;
  tokens: Map<string, { usuarioId: string; valido: boolean }>;
  /** Quem o cookie de refresh identifica (o mock não depende de cookie real). */
  sessaoRefresh: string | null;
  contadores: { refresh: number };
  falhas: FalhaInjetada[];
}

export const SENHA_MOCK = "apolice-demo";
const CHAVE_SESSAO_DEV = "apolice.mock.sessao";
const DIAS_VALIDADE_COTACAO = 30;

/* ---------------------------------------------------------------- ids e números */

const SEMENTE = 20_261_021;
let sorteio: (() => number) | null = null;

/** mulberry32: o seed gera sempre os mesmos ids, então um link /apolices/{id} sobrevive à recarga. */
function geradorComSemente(semente: number): () => number {
  let estado = semente;
  return () => {
    estado = (estado + 0x6d2b79f5) | 0;
    let t = Math.imul(estado ^ (estado >>> 15), 1 | estado);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4_294_967_296;
  };
}

function uuid(): string {
  const proximo = sorteio;
  if (!proximo) return crypto.randomUUID();
  const hex = Array.from({ length: 32 }, () => Math.floor(proximo() * 16).toString(16)).join("");
  const variante = (8 + Math.floor(proximo() * 4)).toString(16);
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(13, 16)}-${variante}${hex.slice(17, 20)}-${hex.slice(20)}`;
}

function proximoNumero(numeradores: Map<string, number>, chave: string, inicial: number): number {
  const atual = numeradores.get(chave) ?? inicial;
  numeradores.set(chave, atual + 1);
  return atual + 1;
}

function numeroApolice(b: Banco, produto: CodigoProduto, data: string): string {
  const seq = proximoNumero(b.numeradores, `APOLICE:${produto}:${String(anoDe(data))}`, 40);
  return `${produto}-${String(anoDe(data))}-${String(seq).padStart(6, "0")}`;
}

function numeroSinistro(b: Banco, data: string): string {
  const seq = proximoNumero(b.numeradores, `SINISTRO:${String(anoDe(data))}`, 90);
  return `SIN-${String(anoDe(data))}-${String(seq).padStart(6, "0")}`;
}

/* ---------------------------------------------------------------- estado */

let banco: Banco = criarBancoVazio();

function criarBancoVazio(): Banco {
  return {
    produtos: criarCatalogo(),
    usuarios: [
      {
        id: uuid(),
        persona: "corretora",
        nome: "Ana Ribeiro",
        email: "ana.corretora@apolice.dev",
        papel: "Corretor",
      },
      {
        id: uuid(),
        persona: "corretor",
        nome: "Bruno Lima",
        email: "bruno.corretor@apolice.dev",
        papel: "Corretor",
      },
      {
        id: uuid(),
        persona: "analista",
        nome: "Carla Souza",
        email: "carla.analista@apolice.dev",
        papel: "Analista",
      },
      {
        id: uuid(),
        persona: "admin",
        nome: "Diego Martins",
        email: "admin@apolice.dev",
        papel: "Admin",
      },
    ],
    cotacoes: [],
    apolices: [],
    sinistros: [],
    notificacoes: [],
    auditoria: [],
    numeradores: new Map(),
    idempotencia: new Map(),
    features: {
      loginDemo: true,
      triagemAutomaticaSinistros: true,
      documentoPdf: true,
      endossoOnline: true,
    },
    config: { atrasoWorkerMs: 1_500, latenciaMs: 0 },
    tokens: new Map(),
    sessaoRefresh: null,
    contadores: { refresh: 0 },
    falhas: [],
  };
}

export function db(): Banco {
  return banco;
}

/** Banco novo com o seed de demonstração; cada teste começa daqui. */
export function resetarBanco(config: Partial<ConfigMock> = {}): Banco {
  sorteio = geradorComSemente(SEMENTE);
  try {
    banco = criarBancoVazio();
    banco.config = { ...banco.config, ...config };
    semear(banco);
  } finally {
    // Depois do seed, o que o usuário cria ganha id aleatório (como o Guid v7 da API).
    sorteio = null;
  }
  return banco;
}

export function usuarioPorPersona(persona: string): UsuarioMock {
  const usuario = banco.usuarios.find((u) => u.persona === persona);
  if (!usuario) throw new Error(`Persona desconhecida no mock: ${persona}`);
  return usuario;
}

/** O que a API devolve do usuário (sem o campo interno de persona). */
export function usuarioDto(usuario: UsuarioMock): UsuarioDto {
  return { id: usuario.id, nome: usuario.nome, email: usuario.email, papel: usuario.papel };
}

export function usuarioPorId(id: string): UsuarioMock | undefined {
  return banco.usuarios.find((u) => u.id === id);
}

/* ---------------------------------------------------------------- sessão (cookie simulado) */

export function definirSessaoRefresh(usuarioId: string | null): void {
  banco.sessaoRefresh = usuarioId;
  try {
    if (typeof sessionStorage === "undefined") return;
    if (usuarioId) {
      const persona = usuarioPorId(usuarioId)?.persona;
      if (persona) sessionStorage.setItem(CHAVE_SESSAO_DEV, persona);
    } else {
      sessionStorage.removeItem(CHAVE_SESSAO_DEV);
    }
  } catch {
    // Storage bloqueado: a sessão do mock vale só até recarregar.
  }
}

/** Modo dev: a recarga recria o banco; a persona logada volta do sessionStorage. */
export function restaurarSessaoDev(): void {
  try {
    const persona = sessionStorage.getItem(CHAVE_SESSAO_DEV);
    if (persona) banco.sessaoRefresh = usuarioPorPersona(persona).id;
  } catch {
    banco.sessaoRefresh = null;
  }
}

export function emitirToken(usuarioId: string): string {
  const token = `mock.${uuid()}`;
  banco.tokens.set(token, { usuarioId, valido: true });
  return token;
}

/** Simula o access token vencido (15 min): a próxima chamada recebe 401 e o app renova. */
export function expirarTokensDeAcesso(): void {
  for (const registro of banco.tokens.values()) registro.valido = false;
}

/** Faz a próxima chamada que casar falhar com ProblemDetails (teste de erro com correlationId). */
export function injetarFalha(
  falha: Omit<FalhaInjetada, "correlationId"> & { correlationId?: string },
): void {
  banco.falhas.push({ correlationId: `corr-${uuid().slice(0, 8)}`, ...falha });
}

/** Fila do worker: efeitos assíncronos chegam depois, como no outbox → fila → consumidor. */
export function agendarNoWorker(efeito: () => void): void {
  setTimeout(efeito, banco.config.atrasoWorkerMs);
}

/* ---------------------------------------------------------------- escopo */

export function veTudo(papel: Papel): boolean {
  return papel === "Analista" || papel === "Admin";
}

export function alcanca(usuario: UsuarioMock, corretorId: string): boolean {
  return veTudo(usuario.papel) || usuario.id === corretorId;
}

/* ---------------------------------------------------------------- DTOs */

export function statusCotacao(c: CotacaoMock, agora = Date.now()): StatusEfetivoCotacao {
  const aberta = c.status === "Calculada" || c.status === "Aceita";
  return aberta && agora > new Date(c.validaAte).getTime() ? "Expirada" : c.status;
}

export function statusApolice(a: ApoliceMock, dia = hoje()): StatusEfetivoApolice {
  if (a.status === "Cancelada") return "Cancelada";
  if (dia < a.vigenciaInicio) return "Programada";
  if (dia > a.vigenciaFim) return "Vencida";
  return "Ativa";
}

const nomeDoUsuario = (id: string) => usuarioPorId(id)?.nome ?? "Usuário";
const produtoPor = (codigo: CodigoProduto) => banco.produtos.find((p) => p.codigo === codigo);

export function produtoDoCodigo(codigo: string): ProdutoDto | undefined {
  return banco.produtos.find((p) => p.codigo === codigo.toUpperCase());
}

export function cotacaoDto(c: CotacaoMock): CotacaoDto {
  return {
    id: c.id,
    produtoCodigo: c.produtoCodigo,
    produtoNome: produtoPor(c.produtoCodigo)?.nome ?? c.produtoCodigo,
    segurado: c.segurado,
    dadosRisco: c.dadosRisco,
    itens: c.tarifacao.itens,
    fatores: c.tarifacao.fatores,
    premioLiquido: c.tarifacao.premioLiquido,
    aliquotaIof: c.tarifacao.aliquotaIof,
    iof: c.tarifacao.iof,
    premioTotal: c.tarifacao.premioTotal,
    maxParcelas: c.tarifacao.maxParcelas,
    status: statusCotacao(c),
    validaAte: c.validaAte,
    criadaEm: c.criadaEm,
    corretorId: c.corretorId,
    corretorNome: nomeDoUsuario(c.corretorId),
    apoliceId: c.apoliceId,
  };
}

export function cotacaoResumo(c: CotacaoMock): CotacaoResumoDto {
  return {
    id: c.id,
    produtoCodigo: c.produtoCodigo,
    seguradoNome: c.segurado.nome,
    premioTotal: c.tarifacao.premioTotal,
    status: statusCotacao(c),
    validaAte: c.validaAte,
    criadaEm: c.criadaEm,
    corretorNome: nomeDoUsuario(c.corretorId),
  };
}

export const ajustesDe = (a: ApoliceMock) =>
  Math.round(a.endossos.reduce((s, e) => s + e.ajusteLiquido, 0) * 100) / 100;

export function sinistroResumo(s: SinistroMock): SinistroResumoDto {
  const apolice = banco.apolices.find((a) => a.id === s.apoliceId);
  return {
    id: s.id,
    numero: s.numero,
    apoliceNumero: apolice?.numero ?? "",
    seguradoNome: apolice?.segurado.nome ?? "",
    coberturaNome: s.coberturaNome,
    valorReclamado: s.valorReclamado,
    status: s.status,
    triagemScore: s.triagem?.score ?? null,
    abertoEm: s.abertoEm,
  };
}

export function apoliceDto(a: ApoliceMock): ApoliceDto {
  const ajustes = ajustesDe(a);
  const sinistros = banco.sinistros.filter((s) => s.apoliceId === a.id);
  const referencias = new Set([a.id, ...sinistros.map((s) => s.id)]);
  return {
    id: a.id,
    numero: a.numero,
    cotacaoId: a.cotacaoId,
    produtoCodigo: a.produtoCodigo,
    produtoNome: produtoPor(a.produtoCodigo)?.nome ?? a.produtoCodigo,
    segurado: a.segurado,
    dadosRisco: a.dadosRisco,
    vigenciaInicio: a.vigenciaInicio,
    vigenciaFim: a.vigenciaFim,
    coberturas: a.coberturas,
    premioLiquido: a.premioLiquido,
    ajustesEndosso: ajustes,
    premioLiquidoVigente: Math.round((a.premioLiquido + ajustes) * 100) / 100,
    aliquotaIof: a.aliquotaIof,
    iof: a.iof,
    premioTotal: a.premioTotal,
    parcelas: a.parcelas,
    cronogramaParcelas: cronograma(a.premioTotal, a.parcelas, a.vigenciaInicio),
    status: statusApolice(a),
    emitidaEm: a.emitidaEm,
    canceladaEm: a.canceladaEm,
    motivoCancelamento: a.motivoCancelamento,
    valorRestituicao: a.valorRestituicao,
    boasVindasEnviadasEm: a.boasVindasEnviadasEm,
    corretorId: a.corretorId,
    corretorNome: nomeDoUsuario(a.corretorId),
    endossos: [...a.endossos].sort((x, y) => x.numero - y.numero),
    sinistros: sinistros.map(sinistroResumo),
    notificacoes: banco.notificacoes
      .filter((n) => referencias.has(n.referenciaId))
      .map((n) => ({
        id: n.id,
        tipo: n.tipo,
        destinatario: n.destinatario,
        assunto: n.assunto,
        corpo: n.corpo,
        criadaEm: n.criadaEm,
      })),
  };
}

export function apoliceResumo(a: ApoliceMock): ApoliceResumoDto {
  return {
    id: a.id,
    numero: a.numero,
    produtoCodigo: a.produtoCodigo,
    seguradoNome: a.segurado.nome,
    seguradoCpfMascarado: mascararCpf(a.segurado.cpf),
    premioTotal: a.premioTotal,
    vigenciaInicio: a.vigenciaInicio,
    vigenciaFim: a.vigenciaFim,
    status: statusApolice(a),
    corretorNome: nomeDoUsuario(a.corretorId),
    emitidaEm: a.emitidaEm,
  };
}

export function sinistroDto(s: SinistroMock): SinistroDto {
  const apolice = banco.apolices.find((a) => a.id === s.apoliceId);
  return {
    id: s.id,
    numero: s.numero,
    apoliceId: s.apoliceId,
    apoliceNumero: apolice?.numero ?? "",
    produtoCodigo: apolice?.produtoCodigo ?? "AUTO",
    seguradoNome: apolice?.segurado.nome ?? "",
    coberturaCodigo: s.coberturaCodigo,
    coberturaNome: s.coberturaNome,
    lmiCobertura: s.lmiCobertura,
    dataOcorrencia: s.dataOcorrencia,
    descricao: s.descricao,
    valorReclamado: s.valorReclamado,
    valorIndenizacao: s.valorIndenizacao,
    status: s.status,
    motivoNegativa: s.motivoNegativa,
    triagem: s.triagem,
    abertoEm: s.abertoEm,
    corretorNome: apolice ? nomeDoUsuario(apolice.corretorId) : "",
    historico: s.historico,
  };
}

/* ---------------------------------------------------------------- efeitos */

export function registrarAuditoria(
  usuario: UsuarioMock | null,
  acao: string,
  entidade: string,
  entidadeId: string | null,
  detalhes: Record<string, unknown> | null,
  correlationId: string | null,
  ocorridoEm = agoraIso(),
): void {
  banco.auditoria.push({
    id: uuid(),
    usuarioNome: usuario?.nome ?? null,
    acao,
    entidade,
    entidadeId,
    detalhes: detalhes ? JSON.stringify(detalhes) : null,
    correlationId,
    ocorridoEm,
  });
}

export function notificar(
  referenciaId: string,
  tipo: TipoNotificacao,
  destinatario: string,
  assunto: string,
  corpo: string,
  criadaEm = agoraIso(),
): void {
  banco.notificacoes.push({
    id: uuid(),
    referenciaId,
    tipo,
    destinatario,
    assunto,
    corpo,
    criadaEm,
  });
}

const ASSINATURA = "Atenciosamente,\nEquipe Apólice";

export function enviarBoasVindas(a: ApoliceMock, quando = agoraIso()): void {
  if (a.boasVindasEnviadasEm) return;
  a.boasVindasEnviadasEm = quando;
  const produto = produtoPor(a.produtoCodigo)?.nome ?? a.produtoCodigo;
  notificar(
    a.id,
    "BoasVindas",
    a.segurado.email,
    `Bem-vindo(a)! Sua apólice ${a.numero} foi emitida`,
    `Olá, ${a.segurado.nome}.\n\nÉ um prazer ter você conosco. Sua apólice ${a.numero} (${produto}) foi emitida com sucesso.\nVigência: de ${dataBr(a.vigenciaInicio)} a ${dataBr(a.vigenciaFim)}.\nPrêmio total: ${moeda(a.premioTotal)}.\n\n${ASSINATURA}`,
    quando,
  );
}

export function triar(s: SinistroMock, quando = agoraIso()): void {
  if (s.triagem) return;
  const apolice = banco.apolices.find((a) => a.id === s.apoliceId);
  if (!apolice) return;
  const { score, alertas } = avaliarTriagem(
    s.dataOcorrencia,
    apolice.vigenciaInicio,
    s.valorReclamado,
    s.lmiCobertura,
    dataDoInstante(s.abertoEm),
  );
  s.triagem = { score, alertas, triadoEm: quando };
}

function dataBr(data: string): string {
  return `${data.slice(8, 10)}/${data.slice(5, 7)}/${data.slice(0, 4)}`;
}

/* ---------------------------------------------------------------- criação (seed e handlers) */

export function criarCotacao(
  corretorId: string,
  produto: ProdutoDto,
  segurado: SeguradoDto,
  dados: DadosRisco,
  tarifacao: TarifacaoDto,
  criadaEm = agoraIso(),
): CotacaoMock {
  const cotacao: CotacaoMock = {
    id: uuid(),
    produtoCodigo: produto.codigo,
    segurado,
    dadosRisco: dados,
    tarifacao,
    status: "Calculada",
    validaAte: new Date(
      new Date(criadaEm).getTime() + DIAS_VALIDADE_COTACAO * 86_400_000,
    ).toISOString(),
    criadaEm,
    corretorId,
    apoliceId: null,
  };
  banco.cotacoes.push(cotacao);
  return cotacao;
}

export function emitirApolice(
  cotacao: CotacaoMock,
  inicio: string,
  parcelas: number,
  emitidaEm = agoraIso(),
): ApoliceMock {
  const apolice: ApoliceMock = {
    id: uuid(),
    numero: numeroApolice(banco, cotacao.produtoCodigo, dataDoInstante(emitidaEm)),
    cotacaoId: cotacao.id,
    produtoCodigo: cotacao.produtoCodigo,
    segurado: cotacao.segurado,
    dadosRisco: cotacao.dadosRisco,
    vigenciaInicio: inicio,
    vigenciaFim: somarMeses(inicio, 12),
    coberturas: cotacao.tarifacao.itens.map((i) => ({ ...i })),
    premioLiquido: cotacao.tarifacao.premioLiquido,
    aliquotaIof: cotacao.tarifacao.aliquotaIof,
    iof: cotacao.tarifacao.iof,
    premioTotal: cotacao.tarifacao.premioTotal,
    parcelas,
    status: "Ativa",
    emitidaEm,
    canceladaEm: null,
    motivoCancelamento: null,
    valorRestituicao: null,
    boasVindasEnviadasEm: null,
    corretorId: cotacao.corretorId,
    endossos: [],
  };
  cotacao.status = "Emitida";
  cotacao.apoliceId = apolice.id;
  banco.apolices.push(apolice);
  return apolice;
}

export function abrirSinistro(
  apolice: ApoliceMock,
  coberturaCodigo: string,
  dataOcorrencia: string,
  descricao: string,
  valorReclamado: number,
  usuarioId: string | null,
  abertoEm = agoraIso(),
): SinistroMock {
  const cobertura = apolice.coberturas.find((c) => c.codigo === coberturaCodigo);
  const sinistro: SinistroMock = {
    id: uuid(),
    numero: numeroSinistro(banco, dataDoInstante(abertoEm)),
    apoliceId: apolice.id,
    coberturaCodigo,
    coberturaNome: cobertura?.nome ?? coberturaCodigo,
    lmiCobertura: cobertura?.lmi ?? 0,
    dataOcorrencia,
    descricao,
    valorReclamado,
    valorIndenizacao: null,
    status: "Aberto",
    motivoNegativa: null,
    triagem: null,
    abertoEm,
    historico: [
      {
        status: "Aberto",
        observacao: null,
        usuarioNome: usuarioId ? nomeDoUsuario(usuarioId) : null,
        ocorridoEm: abertoEm,
      },
    ],
  };
  banco.sinistros.push(sinistro);
  return sinistro;
}

export function notificarSinistroAberto(s: SinistroMock, quando = agoraIso()): void {
  const apolice = banco.apolices.find((a) => a.id === s.apoliceId);
  if (!apolice) return;
  notificar(
    s.id,
    "SinistroAberto",
    apolice.segurado.email,
    `Recebemos o aviso do sinistro ${s.numero}`,
    `Olá, ${apolice.segurado.nome}.\n\nRecebemos o aviso do sinistro ${s.numero}, referente à apólice ${apolice.numero} (cobertura ${s.coberturaNome}), ocorrido em ${dataBr(s.dataOcorrencia)}.\nValor reclamado: ${moeda(s.valorReclamado)}.\n\n${ASSINATURA}`,
    quando,
  );
}

export function transicionar(
  s: SinistroMock,
  status: StatusSinistro,
  usuarioNome: string,
  observacao: string | null,
  quando = agoraIso(),
): void {
  s.status = status;
  s.historico.push({ status, observacao, usuarioNome, ocorridoEm: quando });
}

export { simularCancelamento, somarDias, hoje, instante, diasEntre };

/* ---------------------------------------------------------------- seed */

interface PlanoApolice {
  corretora: "corretora" | "corretor";
  produto: CodigoProduto;
  segurado: SeguradoDto;
  dados: DadosRisco;
  coberturas: { codigo: string; lmi: number | null }[];
  /** Emitida há N dias; início da vigência = emissão + deslocamento. */
  emitidaHaDias: number;
  inicioAposEmissao?: number;
  parcelas: number;
}

function semearApolice(b: Banco, plano: PlanoApolice): ApoliceMock {
  const produto = b.produtos.find((p) => p.codigo === plano.produto);
  if (!produto) throw new Error(`Produto ${plano.produto} fora do catálogo.`);
  const corretor = usuarioPorPersona(plano.corretora);
  const diaEmissao = somarDias(hoje(), -plano.emitidaHaDias);
  const criadaEm = instante(somarDias(diaEmissao, -2), "09:40");
  const tarifacao = tarifar(produto, plano.segurado, plano.dados, plano.coberturas, diaEmissao);
  const cotacao = criarCotacao(
    corretor.id,
    produto,
    plano.segurado,
    plano.dados,
    tarifacao,
    criadaEm,
  );
  cotacao.status = "Aceita";
  const emitidaEm = instante(diaEmissao, "09:12");
  const apolice = emitirApolice(
    cotacao,
    somarDias(diaEmissao, plano.inicioAposEmissao ?? 0),
    plano.parcelas,
    emitidaEm,
  );
  registrarAuditoria(
    corretor,
    ACOES_AUDITORIA.ApoliceEmitida,
    ENTIDADES_AUDITORIA.Apolice,
    apolice.id,
    { numero: apolice.numero },
    null,
    emitidaEm,
  );
  enviarBoasVindas(apolice, instante(diaEmissao, "09:13"));
  return apolice;
}

const SEGURADOS: Record<string, SeguradoDto> = {
  rafael: {
    nome: "Rafael Nogueira Castro",
    cpf: "529.982.247-25",
    email: "rafael.castro@exemplo.com.br",
    dataNascimento: "1985-04-12",
  },
  patricia: {
    nome: "Patrícia Menezes",
    cpf: "111.444.777-35",
    email: "patricia.menezes@exemplo.com.br",
    dataNascimento: "1979-09-30",
  },
  helena: {
    nome: "Helena Duarte",
    cpf: "390.533.447-05",
    email: "helena.duarte@exemplo.com.br",
    dataNascimento: "1990-02-18",
  },
  marcelo: {
    nome: "Marcelo Tavares",
    cpf: "153.509.460-56",
    email: "marcelo.tavares@exemplo.com.br",
    dataNascimento: "1983-11-02",
  },
  juliana: {
    nome: "Juliana Prado",
    cpf: "714.602.380-01",
    email: "juliana.prado@exemplo.com.br",
    dataNascimento: "1995-06-21",
  },
  gustavo: {
    nome: "Gustavo Ferraz",
    cpf: "868.645.030-51",
    email: "gustavo.ferraz@exemplo.com.br",
    dataNascimento: "1972-01-08",
  },
  carolina: {
    nome: "Carolina Bastos",
    cpf: "045.316.770-04",
    email: "carolina.bastos@exemplo.com.br",
    dataNascimento: "1988-07-14",
  },
  rodrigo: {
    nome: "Rodrigo Saldanha",
    cpf: "275.484.389-23",
    email: "rodrigo.saldanha@exemplo.com.br",
    dataNascimento: "1976-03-27",
  },
  fernanda: {
    nome: "Fernanda Lima",
    cpf: "862.411.590-04",
    email: "fernanda.lima@exemplo.com.br",
    dataNascimento: "1992-12-05",
  },
  lucas: {
    nome: "Lucas Andrade",
    cpf: "481.073.950-04",
    email: "lucas.andrade@exemplo.com.br",
    dataNascimento: "2004-05-10",
  },
  beatriz: {
    nome: "Beatriz Moura",
    cpf: "317.665.180-92",
    email: "beatriz.moura@exemplo.com.br",
    dataNascimento: "1987-08-19",
  },
  thiago: {
    nome: "Thiago Rezende",
    cpf: "604.818.250-90",
    email: "thiago.rezende@exemplo.com.br",
    dataNascimento: "1999-10-03",
  },
};

function seg(chave: keyof typeof SEGURADOS): SeguradoDto {
  const segurado = SEGURADOS[chave];
  if (!segurado) throw new Error(`Segurado ${chave} fora do seed.`);
  return segurado;
}

function semear(b: Banco): void {
  const ana = usuarioPorPersona("corretora");
  const bruno = usuarioPorPersona("corretor");
  const carla = usuarioPorPersona("analista");

  const rafael = semearApolice(b, {
    corretora: "corretora",
    produto: "AUTO",
    segurado: seg("rafael"),
    dados: {
      produto: "AUTO",
      valorVeiculo: 141_900,
      anoFabricacao: anoDe(hoje()) - 1,
      cepPernoite: "41820021",
      idadeCondutor: 41,
      usoComercial: false,
      classeBonus: 3,
    },
    coberturas: [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 100_000 },
      { codigo: "RCF_DC", lmi: 100_000 },
      { codigo: "VIDROS", lmi: null },
      { codigo: "ASSISTENCIA_24H", lmi: null },
    ],
    emitidaHaDias: 220,
    parcelas: 10,
  });
  const patricia = semearApolice(b, {
    corretora: "corretora",
    produto: "RESID",
    segurado: seg("patricia"),
    dados: {
      produto: "RESID",
      valorImovel: 620_000,
      tipoImovel: "Apartamento",
      cep: "40150110",
      habitual: true,
    },
    coberturas: [
      { codigo: "INCENDIO", lmi: 620_000 },
      { codigo: "DANOS_ELETRICOS", lmi: 15_000 },
      { codigo: "ROUBO", lmi: 30_000 },
    ],
    emitidaHaDias: 340,
    parcelas: 4,
  });
  semearApolice(b, {
    corretora: "corretora",
    produto: "VIDA",
    segurado: seg("helena"),
    dados: { produto: "VIDA", fumante: false },
    coberturas: [
      { codigo: "MORTE", lmi: 300_000 },
      { codigo: "IPA", lmi: 150_000 },
      { codigo: "FUNERAL", lmi: null },
    ],
    emitidaHaDias: 352,
    parcelas: 3,
  });
  semearApolice(b, {
    corretora: "corretora",
    produto: "AUTO",
    segurado: seg("marcelo"),
    dados: {
      produto: "AUTO",
      valorVeiculo: 189_500,
      anoFabricacao: anoDe(hoje()),
      cepPernoite: "01310100",
      idadeCondutor: 42,
      usoComercial: false,
      classeBonus: 5,
    },
    coberturas: [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 150_000 },
      { codigo: "VIDROS", lmi: null },
      { codigo: "CARRO_RESERVA", lmi: null },
    ],
    emitidaHaDias: 6,
    parcelas: 6,
  });
  semearApolice(b, {
    corretora: "corretora",
    produto: "AUTO",
    segurado: seg("juliana"),
    dados: {
      produto: "AUTO",
      valorVeiculo: 98_700,
      anoFabricacao: anoDe(hoje()) - 3,
      cepPernoite: "22041001",
      idadeCondutor: 31,
      usoComercial: false,
      classeBonus: 1,
    },
    coberturas: [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 50_000 },
      { codigo: "ASSISTENCIA_24H", lmi: null },
    ],
    emitidaHaDias: 2,
    inicioAposEmissao: 12,
    parcelas: 10,
  });
  const gustavo = semearApolice(b, {
    corretora: "corretora",
    produto: "RESID",
    segurado: seg("gustavo"),
    dados: {
      produto: "RESID",
      valorImovel: 450_000,
      tipoImovel: "Casa",
      cep: "41940310",
      habitual: true,
    },
    coberturas: [
      { codigo: "INCENDIO", lmi: 450_000 },
      { codigo: "ROUBO", lmi: 40_000 },
      { codigo: "VENDAVAL", lmi: 50_000 },
    ],
    emitidaHaDias: 150,
    parcelas: 5,
  });
  semearApolice(b, {
    corretora: "corretora",
    produto: "VIDA",
    segurado: seg("carolina"),
    dados: { produto: "VIDA", fumante: true },
    coberturas: [
      { codigo: "MORTE", lmi: 200_000 },
      { codigo: "FUNERAL", lmi: null },
    ],
    emitidaHaDias: 400,
    parcelas: 2,
  });
  const rodrigo = semearApolice(b, {
    corretora: "corretor",
    produto: "AUTO",
    segurado: seg("rodrigo"),
    dados: {
      produto: "AUTO",
      valorVeiculo: 76_300,
      anoFabricacao: anoDe(hoje()) - 6,
      cepPernoite: "30140071",
      idadeCondutor: 50,
      usoComercial: true,
      classeBonus: 2,
    },
    coberturas: [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 100_000 },
      { codigo: "VIDROS", lmi: null },
    ],
    emitidaHaDias: 300,
    parcelas: 8,
  });
  const fernanda = semearApolice(b, {
    corretora: "corretor",
    produto: "RESID",
    segurado: seg("fernanda"),
    dados: {
      produto: "RESID",
      valorImovel: 380_000,
      tipoImovel: "Apartamento",
      cep: "04538133",
      habitual: true,
    },
    coberturas: [
      { codigo: "INCENDIO", lmi: 380_000 },
      { codigo: "DANOS_ELETRICOS", lmi: 10_000 },
    ],
    emitidaHaDias: 90,
    parcelas: 2,
  });
  const lucas = semearApolice(b, {
    corretora: "corretor",
    produto: "VIDA",
    segurado: seg("lucas"),
    dados: { produto: "VIDA", fumante: false },
    coberturas: [
      { codigo: "MORTE", lmi: 100_000 },
      { codigo: "IPA", lmi: 100_000 },
    ],
    emitidaHaDias: 60,
    parcelas: 1,
  });

  // Endosso nº 1 (alteração de LMI) e nº 2 (inclusão de cobertura).
  semearEndosso(rafael, "AlteracaoLmi", "RCF_DM", 150_000, 130, ana);
  semearEndosso(fernanda, "InclusaoCobertura", "VENDAVAL", 30_000, 40, bruno);

  // Cancelamento a pedido do segurado (prazo curto).
  const diaCancelamento = somarDias(hoje(), -40);
  const simulacao = simularCancelamento(
    gustavo.premioLiquido + ajustesDe(gustavo),
    gustavo.vigenciaInicio,
    gustavo.vigenciaFim,
    "PedidoDoSegurado",
    diaCancelamento,
  );
  gustavo.status = "Cancelada";
  gustavo.canceladaEm = instante(diaCancelamento, "16:20");
  gustavo.motivoCancelamento = "PedidoDoSegurado";
  gustavo.valorRestituicao = simulacao.valorRestituicao;
  notificar(
    gustavo.id,
    "Cancelamento",
    gustavo.segurado.email,
    `Cancelamento da apólice ${gustavo.numero}`,
    `Olá, ${gustavo.segurado.nome}.\n\nConfirmamos o cancelamento da sua apólice ${gustavo.numero}, por pedido do segurado.\nValor a restituir: ${moeda(simulacao.valorRestituicao)} (o IOF não é restituível).\n\n${ASSINATURA}`,
    instante(diaCancelamento, "16:21"),
  );

  // Sinistros em todos os status.
  const s1 = abrirSinistro(
    rafael,
    "CASCO",
    somarDias(hoje(), -3),
    "Colisão traseira em semáforo na Av. Tancredo Neves, Salvador/BA. Para-choque e lanterna danificados.",
    8_420,
    ana.id,
    instante(somarDias(hoje(), -1), "11:47"),
  );
  triar(s1, instante(somarDias(hoje(), -1), "11:48"));
  notificarSinistroAberto(s1, instante(somarDias(hoje(), -1), "11:48"));
  transicionar(s1, "EmAnalise", carla.nome, null, instante(hoje(), "08:55"));

  const s2 = abrirSinistro(
    patricia,
    "ROUBO",
    somarDias(hoje(), -45),
    "Arrombamento da porta de serviço com furto de notebook e televisor. Boletim de ocorrência registrado.",
    7_800,
    ana.id,
    instante(somarDias(hoje(), -44), "15:02"),
  );
  triar(s2, instante(somarDias(hoje(), -44), "15:03"));
  notificarSinistroAberto(s2, instante(somarDias(hoje(), -44), "15:03"));
  transicionar(s2, "EmAnalise", carla.nome, null, instante(somarDias(hoje(), -40), "10:10"));
  transicionar(
    s2,
    "Aprovado",
    carla.nome,
    "Documentação conferida; o televisor sem nota fiscal ficou fora.",
    instante(somarDias(hoje(), -30), "17:35"),
  );
  s2.valorIndenizacao = 6_400;

  const s3 = abrirSinistro(
    rodrigo,
    "VIDROS",
    somarDias(hoje(), -120),
    "Trinca no para-brisa causada por pedra lançada por caminhão na BR-040.",
    2_600,
    bruno.id,
    instante(somarDias(hoje(), -119), "09:30"),
  );
  triar(s3, instante(somarDias(hoje(), -119), "09:31"));
  transicionar(s3, "EmAnalise", carla.nome, null, instante(somarDias(hoje(), -118), "14:00"));
  transicionar(s3, "Aprovado", carla.nome, null, instante(somarDias(hoje(), -117), "16:45"));
  s3.valorIndenizacao = 2_600;
  transicionar(s3, "Pago", carla.nome, null, instante(somarDias(hoje(), -110), "11:00"));

  const s4 = abrirSinistro(
    fernanda,
    "DANOS_ELETRICOS",
    somarDias(hoje(), -20),
    "Queima de geladeira e roteador após queda de energia no condomínio durante a madrugada.",
    9_800,
    bruno.id,
    instante(somarDias(hoje(), -19), "08:20"),
  );
  triar(s4, instante(somarDias(hoje(), -19), "08:21"));
  transicionar(s4, "EmAnalise", carla.nome, null, instante(somarDias(hoje(), -18), "09:00"));
  transicionar(
    s4,
    "Negado",
    carla.nome,
    "Laudo técnico apontou desgaste natural do compressor, sem relação com a oscilação de energia.",
    instante(somarDias(hoje(), -12), "15:30"),
  );
  s4.motivoNegativa =
    "Laudo técnico apontou desgaste natural do compressor, sem relação com a oscilação de energia.";

  const s5 = abrirSinistro(
    lucas,
    "IPA",
    somarDias(hoje(), -50),
    "Queda de moto com fratura no punho direito; perícia médica agendada para avaliar sequela permanente.",
    95_000,
    bruno.id,
    instante(somarDias(hoje(), -2), "16:05"),
  );
  triar(s5, instante(somarDias(hoje(), -2), "16:06"));
  notificarSinistroAberto(s5, instante(somarDias(hoje(), -2), "16:06"));

  semearCotacoes(b, ana.id, bruno.id);
  b.auditoria.sort((x, y) => x.ocorridoEm.localeCompare(y.ocorridoEm));
}

function semearEndosso(
  apolice: ApoliceMock,
  tipo: EndossoDto["tipo"],
  codigo: string,
  lmi: number,
  haDias: number,
  autor: UsuarioMock,
): void {
  const produto = produtoPor(apolice.produtoCodigo);
  if (!produto) return;
  const dataEfeito = somarDias(hoje(), -haDias);
  const atual = apolice.coberturas.find((c) => c.codigo === codigo);
  const pedido = [
    ...apolice.coberturas
      .filter((c) => c.codigo !== codigo)
      .map((c) => ({ codigo: c.codigo, lmi: c.lmi })),
    { codigo, lmi },
  ];
  const t = tarifar(
    produto,
    apolice.segurado,
    apolice.dadosRisco,
    pedido,
    dataDoInstante(apolice.emitidaEm),
  );
  const novo = t.itens.find((i) => i.codigo === codigo);
  if (!novo) return;
  const diasTotais = diasEntre(apolice.vigenciaInicio, apolice.vigenciaFim);
  const restantes = diasEntre(dataEfeito, apolice.vigenciaFim);
  const anterior = atual?.premioLiquido ?? 0;
  const ajusteLiquido =
    Math.round((((novo.premioLiquido - anterior) * restantes) / diasTotais) * 100) / 100;
  const iofAjuste = Math.round(ajusteLiquido * apolice.aliquotaIof * 100) / 100;
  const criadoEm = instante(dataEfeito, "15:40");
  apolice.endossos.push({
    id: uuid(),
    numero: apolice.endossos.length + 1,
    tipo,
    coberturaCodigo: codigo,
    coberturaNome: novo.nome,
    lmiAnterior: atual?.lmi ?? null,
    lmiNovo: lmi,
    dataEfeito,
    ajusteLiquido,
    iofAjuste,
    ajusteTotal: Math.round((ajusteLiquido + iofAjuste) * 100) / 100,
    criadoEm,
    criadoPorNome: autor.nome,
  });
  apolice.coberturas = atual
    ? apolice.coberturas.map((c) => (c.codigo === codigo ? { ...novo } : c))
    : [...apolice.coberturas, { ...novo }];
  notificar(
    apolice.id,
    "Endosso",
    apolice.segurado.email,
    `Endosso nº ${String(apolice.endossos.length)} da apólice ${apolice.numero}`,
    `Olá, ${apolice.segurado.nome}.\n\nRegistramos o endosso nº ${String(apolice.endossos.length)} na sua apólice ${apolice.numero}, com efeito a partir de ${dataBr(dataEfeito)}.\n\n${ASSINATURA}`,
    instante(dataEfeito, "15:41"),
  );
  registrarAuditoria(
    autor,
    ACOES_AUDITORIA.ApoliceEndossada,
    ENTIDADES_AUDITORIA.Apolice,
    apolice.id,
    { numero: apolice.numero, endosso: apolice.endossos.length },
    null,
    criadoEm,
  );
}

function semearCotacoes(b: Banco, anaId: string, brunoId: string): void {
  const auto = b.produtos.find((p) => p.codigo === "AUTO");
  const resid = b.produtos.find((p) => p.codigo === "RESID");
  const vida = b.produtos.find((p) => p.codigo === "VIDA");
  if (!auto || !resid || !vida) return;
  const ref = hoje();
  const nova = (
    corretorId: string,
    produto: ProdutoDto,
    segurado: SeguradoDto,
    dados: DadosRisco,
    coberturas: { codigo: string; lmi: number | null }[],
    criadaHaDias: number,
    hora: string,
  ) =>
    criarCotacao(
      corretorId,
      produto,
      segurado,
      dados,
      tarifar(produto, segurado, dados, coberturas, ref),
      instante(somarDias(ref, -criadaHaDias), hora),
    );

  // Expira em menos de 48 h (fila "Cotações expirando").
  nova(
    anaId,
    auto,
    seg("beatriz"),
    {
      produto: "AUTO",
      valorVeiculo: 112_000,
      anoFabricacao: anoDe(ref) - 2,
      cepPernoite: "41820021",
      idadeCondutor: 37,
      usoComercial: false,
      classeBonus: 4,
    },
    [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 100_000 },
      { codigo: "VIDROS", lmi: null },
    ],
    29,
    "11:20",
  );
  nova(
    anaId,
    vida,
    seg("thiago"),
    { produto: "VIDA", fumante: false },
    [
      { codigo: "MORTE", lmi: 250_000 },
      { codigo: "FUNERAL", lmi: null },
    ],
    1,
    "16:44",
  );
  const aceitaAna = nova(
    anaId,
    resid,
    seg("juliana"),
    {
      produto: "RESID",
      valorImovel: 540_000,
      tipoImovel: "Apartamento",
      cep: "41820021",
      habitual: true,
    },
    [
      { codigo: "INCENDIO", lmi: 540_000 },
      { codigo: "ROUBO", lmi: 25_000 },
    ],
    0,
    "13:05",
  );
  aceitaAna.status = "Aceita";
  const aceitaBruno = nova(
    brunoId,
    auto,
    seg("thiago"),
    {
      produto: "AUTO",
      valorVeiculo: 64_900,
      anoFabricacao: anoDe(ref) - 4,
      cepPernoite: "20040002",
      idadeCondutor: 27,
      usoComercial: false,
      classeBonus: 0,
    },
    [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 50_000 },
    ],
    3,
    "10:02",
  );
  aceitaBruno.status = "Aceita";
  const recusada = nova(
    anaId,
    auto,
    seg("marcelo"),
    {
      produto: "AUTO",
      valorVeiculo: 230_000,
      anoFabricacao: anoDe(ref),
      cepPernoite: "01310100",
      idadeCondutor: 42,
      usoComercial: true,
      classeBonus: 0,
    },
    [
      { codigo: "CASCO", lmi: null },
      { codigo: "RCF_DM", lmi: 200_000 },
    ],
    12,
    "09:15",
  );
  recusada.status = "Recusada";
  // Expirada (validade de 30 dias vencida).
  nova(
    anaId,
    resid,
    seg("beatriz"),
    {
      produto: "RESID",
      valorImovel: 300_000,
      tipoImovel: "Casa",
      cep: "45000000",
      habitual: false,
    },
    [
      { codigo: "INCENDIO", lmi: 300_000 },
      { codigo: "ROUBO", lmi: 20_000 },
    ],
    40,
    "14:30",
  );
  nova(
    brunoId,
    vida,
    seg("fernanda"),
    { produto: "VIDA", fumante: false },
    [
      { codigo: "MORTE", lmi: 120_000 },
      { codigo: "IPA", lmi: 60_000 },
    ],
    5,
    "17:10",
  );
}

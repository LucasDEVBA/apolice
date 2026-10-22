/**
 * Códigos de máquina da trilha de auditoria, iguais aos da API (AcoesAuditoria e
 * EntidadesAuditoria em Apolice.Application/Abstractions/Services). O mock grava estes mesmos.
 */
export const ACOES_AUDITORIA = {
  Login: "auth.login",
  LoginDemo: "auth.login_demo",
  LoginFalhou: "auth.login_falhou",
  UsuarioBloqueado: "auth.usuario_bloqueado",
  LoginBloqueado: "auth.login_bloqueado",
  LoginInativo: "auth.login_inativo",
  RefreshReutilizado: "auth.refresh_reutilizado",
  SessaoRevogadaUsuarioInativo: "auth.sessao_revogada_inativo",
  Logout: "auth.logout",
  TaxaAtualizada: "produto.taxa_atualizada",
  CotacaoCriada: "cotacao.criada",
  CotacaoAceita: "cotacao.aceita",
  CotacaoRecusada: "cotacao.recusada",
  ApoliceEmitida: "apolice.emitida",
  ApoliceEndossada: "apolice.endossada",
  ApoliceCancelada: "apolice.cancelada",
  SinistroAberto: "sinistro.aberto",
  SinistroEmAnalise: "sinistro.em_analise",
  SinistroAprovado: "sinistro.aprovado",
  SinistroNegado: "sinistro.negado",
  SinistroPago: "sinistro.pago",
  SinistroTriado: "sinistro.triado",
} as const;

export type AcaoAuditoria = (typeof ACOES_AUDITORIA)[keyof typeof ACOES_AUDITORIA];

export const ENTIDADES_AUDITORIA = {
  Usuario: "Usuario",
  Produto: "Produto",
  Cotacao: "Cotacao",
  Apolice: "Apolice",
  Sinistro: "Sinistro",
} as const;

export type EntidadeAuditoria = (typeof ENTIDADES_AUDITORIA)[keyof typeof ENTIDADES_AUDITORIA];

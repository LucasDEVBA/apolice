import type {
  MetodoCancelamento,
  MotivoCancelamento,
  TipoCalculo,
  TipoEndosso,
  TipoNotificacao,
} from "@/api/contratos";
import {
  ACOES_AUDITORIA as A,
  ENTIDADES_AUDITORIA as E,
  type AcaoAuditoria,
  type EntidadeAuditoria,
} from "@/api/auditoria-codigos";

/** Rótulos de negócio dos enums da API (o JSON traz o nome C#). */
export const ROTULO_MOTIVO_CANCELAMENTO: Record<MotivoCancelamento, string> = {
  PedidoDoSegurado: "Pedido do segurado",
  IniciativaSeguradora: "Iniciativa da seguradora",
  Inadimplencia: "Inadimplência",
};

export const DESCRICAO_MOTIVO_CANCELAMENTO: Record<MotivoCancelamento, string> = {
  PedidoDoSegurado: "Restituição pela tabela de prazo curto",
  IniciativaSeguradora: "Restituição pro rata dos dias restantes",
  Inadimplencia: "Restituição pro rata dos dias restantes",
};

export const ROTULO_METODO_CANCELAMENTO: Record<MetodoCancelamento, string> = {
  Integral: "Integral",
  PrazoCurto: "Prazo curto",
  ProRataDie: "Pro rata die",
};

export const ROTULO_TIPO_ENDOSSO: Record<TipoEndosso, string> = {
  AlteracaoLmi: "Alteração de LMI",
  InclusaoCobertura: "Inclusão de cobertura",
  ExclusaoCobertura: "Exclusão de cobertura",
};

export const ROTULO_TIPO_NOTIFICACAO: Record<TipoNotificacao, string> = {
  BoasVindas: "Boas-vindas",
  Endosso: "Endosso",
  Cancelamento: "Cancelamento",
  SinistroAberto: "Sinistro aberto",
  SinistroDecidido: "Decisão de sinistro",
};

export const ROTULO_TIPO_CALCULO: Record<TipoCalculo, string> = {
  TaxaSobreLmi: "Taxa sobre o LMI",
  TaxaPorMil: "Taxa por mil (‰)",
  PremioFixo: "Prêmio fixo",
};

/** Alertas da triagem automática (AvaliadorTriagem, SPEC §4.5), na voz do negócio. */
export const ROTULO_ALERTA_TRIAGEM: Record<string, string> = {
  VIGENCIA_RECENTE: "Ocorrência até 30 dias após o início da vigência",
  VALOR_PROXIMO_LMI: "Valor reclamado acima de 80% do LMI",
  VALOR_ACIMA_LMI: "Valor reclamado acima do LMI",
  COMUNICACAO_TARDIA: "Aviso mais de 30 dias depois da ocorrência",
};

export function rotuloAlerta(codigo: string): string {
  return ROTULO_ALERTA_TRIAGEM[codigo] ?? codigo;
}

/**
 * A cotação não tem número de negócio no domínio (só o Guid): a referência curta usa o fim do id,
 * que é a parte aleatória do UUID v7 (o início é o carimbo de tempo e se repete entre registros).
 */
export function referenciaCurta(id: string): string {
  return id.replaceAll("-", "").slice(-8).toUpperCase();
}

/** Ações da auditoria na voz do negócio; um código novo da API aparece cru até ganhar rótulo. */
const ROTULO_ACAO_AUDITORIA: Record<AcaoAuditoria, string> = {
  [A.Login]: "Login",
  [A.LoginDemo]: "Login de demonstração",
  [A.LoginFalhou]: "Login recusado",
  [A.UsuarioBloqueado]: "Usuário bloqueado por tentativas",
  [A.LoginBloqueado]: "Login durante o bloqueio",
  [A.LoginInativo]: "Login de usuário inativo",
  [A.RefreshReutilizado]: "Reuso de refresh token (sessões revogadas)",
  [A.SessaoRevogadaUsuarioInativo]: "Sessão revogada: usuário inativo",
  [A.Logout]: "Logout",
  [A.TaxaAtualizada]: "Taxa de cobertura atualizada",
  [A.CotacaoCriada]: "Cotação criada",
  [A.CotacaoAceita]: "Cotação aceita",
  [A.CotacaoRecusada]: "Cotação recusada",
  [A.ApoliceEmitida]: "Apólice emitida",
  [A.ApoliceEndossada]: "Apólice endossada",
  [A.ApoliceCancelada]: "Apólice cancelada",
  [A.SinistroAberto]: "Sinistro aberto",
  [A.SinistroEmAnalise]: "Sinistro em análise",
  [A.SinistroAprovado]: "Sinistro aprovado",
  [A.SinistroNegado]: "Sinistro negado",
  [A.SinistroPago]: "Sinistro pago",
  [A.SinistroTriado]: "Sinistro triado",
};

const ROTULO_ENTIDADE_AUDITORIA: Record<EntidadeAuditoria, string> = {
  [E.Usuario]: "Usuário",
  [E.Produto]: "Produto",
  [E.Cotacao]: "Cotação",
  [E.Apolice]: "Apólice",
  [E.Sinistro]: "Sinistro",
};

export function rotuloAcaoAuditoria(codigo: string): string {
  return (ROTULO_ACAO_AUDITORIA as Record<string, string | undefined>)[codigo] ?? codigo;
}

export function rotuloEntidadeAuditoria(codigo: string): string {
  return (ROTULO_ENTIDADE_AUDITORIA as Record<string, string | undefined>)[codigo] ?? codigo;
}

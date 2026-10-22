import { toast } from "sonner";

import { anunciar } from "../utils/anunciar";
import { CartaoToast, type OpcoesToast, type TipoToast } from "./CartaoToast";

const DURACAO_PADRAO_MS = 5_000;
const DURACAO_ALERTA_MS = 7_000;

export interface ExtrasToast {
  /** Id fixo: o mesmo aviso disparado de novo substitui o anterior em vez de empilhar. */
  id?: string;
}

function mostrar(tipo: TipoToast, opcoes: OpcoesToast, duracao: number, extras: ExtrasToast) {
  // A região do sonner é polite; erro também vai para a região assertive (DESIGN.md §4.18).
  if (tipo === "erro") anunciar(`${opcoes.titulo}. ${opcoes.descricao ?? ""}`, "assertive");
  return toast.custom((id) => <CartaoToast id={id} tipo={tipo} {...opcoes} />, {
    duration: duracao,
    ...(extras.id ? { id: extras.id } : {}),
  });
}

/** API de notificações: sucesso/info 5 s, alerta 7 s, erro persistente até fechar. */
export const notificar = {
  sucesso: (opcoes: OpcoesToast, extras: ExtrasToast = {}) =>
    mostrar("sucesso", opcoes, DURACAO_PADRAO_MS, extras),
  info: (opcoes: OpcoesToast, extras: ExtrasToast = {}) =>
    mostrar("info", opcoes, DURACAO_PADRAO_MS, extras),
  alerta: (opcoes: OpcoesToast, extras: ExtrasToast = {}) =>
    mostrar("alerta", opcoes, DURACAO_ALERTA_MS, extras),
  erro: (opcoes: OpcoesToast, extras: ExtrasToast = {}) =>
    mostrar("erro", opcoes, Number.POSITIVE_INFINITY, extras),
};

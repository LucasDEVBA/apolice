import { toast } from "sonner";

import { anunciar } from "../utils/anunciar";
import { CartaoToast, type OpcoesToast, type TipoToast } from "./CartaoToast";

const DURACAO_PADRAO_MS = 5_000;
const DURACAO_ALERTA_MS = 7_000;

function mostrar(tipo: TipoToast, opcoes: OpcoesToast, duracao: number) {
  // A região do sonner é polite; erro também vai para a região assertive (DESIGN.md §4.18).
  if (tipo === "erro") anunciar(`${opcoes.titulo}. ${opcoes.descricao ?? ""}`, "assertive");
  return toast.custom((id) => <CartaoToast id={id} tipo={tipo} {...opcoes} />, {
    duration: duracao,
  });
}

/** API de notificações: sucesso/info 5 s, alerta 7 s, erro persistente até fechar. */
export const notificar = {
  sucesso: (opcoes: OpcoesToast) => mostrar("sucesso", opcoes, DURACAO_PADRAO_MS),
  info: (opcoes: OpcoesToast) => mostrar("info", opcoes, DURACAO_PADRAO_MS),
  alerta: (opcoes: OpcoesToast) => mostrar("alerta", opcoes, DURACAO_ALERTA_MS),
  erro: (opcoes: OpcoesToast) => mostrar("erro", opcoes, Number.POSITIVE_INFINITY),
};

import { Check, type LucideIcon } from "lucide-react";

export type Tom = "ok" | "warn" | "bad" | "info" | "neutral";

/* ---------- Status de domínio → tom (DESIGN.md §3.6) ---------- */

export type StatusApolice = "Ativa" | "Programada" | "Vencida" | "Cancelada";
export type StatusCotacao = "Calculada" | "Aceita" | "Emitida" | "Recusada" | "Expirada";
export type StatusSinistro = "Aberto" | "EmAnalise" | "Aprovado" | "Negado" | "Pago";

export interface Apresentacao {
  tom: Tom;
  rotulo: string;
  icone?: LucideIcon;
}

const APOLICE: Record<StatusApolice, Apresentacao> = {
  Ativa: { tom: "ok", rotulo: "Ativa" },
  Programada: { tom: "info", rotulo: "Programada" },
  Vencida: { tom: "neutral", rotulo: "Vencida" },
  Cancelada: { tom: "bad", rotulo: "Cancelada" },
};

const COTACAO: Record<StatusCotacao, Apresentacao> = {
  Calculada: { tom: "neutral", rotulo: "Calculada" },
  Aceita: { tom: "info", rotulo: "Aceita" },
  Emitida: { tom: "ok", rotulo: "Emitida" },
  Recusada: { tom: "bad", rotulo: "Recusada" },
  Expirada: { tom: "warn", rotulo: "Expirada" },
};

const SINISTRO: Record<StatusSinistro, Apresentacao> = {
  Aberto: { tom: "info", rotulo: "Aberto" },
  EmAnalise: { tom: "warn", rotulo: "Em análise" },
  Aprovado: { tom: "ok", rotulo: "Aprovado" },
  Negado: { tom: "bad", rotulo: "Negado" },
  // Pago se distingue de Aprovado pelo ícone e pelo rótulo, não só pela cor.
  Pago: { tom: "ok", rotulo: "Pago", icone: Check },
};

export type StatusBadgeProps = (
  | { entidade: "apolice"; status: StatusApolice }
  | { entidade: "cotacao"; status: StatusCotacao }
  | { entidade: "sinistro"; status: StatusSinistro }
) & { sobreMarinho?: boolean; className?: string };

export function apresentacaoDoStatus(props: StatusBadgeProps): Apresentacao {
  switch (props.entidade) {
    case "apolice":
      return APOLICE[props.status];
    case "cotacao":
      return COTACAO[props.status];
    case "sinistro":
      return SINISTRO[props.status];
  }
}

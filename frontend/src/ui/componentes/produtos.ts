import { Car, HeartPulse, House, type LucideIcon } from "lucide-react";

export type CodigoProduto = "AUTO" | "RESID" | "VIDA";

export interface ApresentacaoProduto {
  nome: string;
  icone: LucideIcon;
  amostra: string;
  chip: string;
}

export const PRODUTOS: Record<CodigoProduto, ApresentacaoProduto> = {
  AUTO: {
    nome: "Auto",
    icone: Car,
    amostra: "bg-prod-auto",
    chip: "bg-prod-auto-chip text-prod-auto-chip-fg",
  },
  RESID: {
    nome: "Residencial",
    icone: House,
    amostra: "bg-prod-resid",
    chip: "bg-prod-resid-chip text-prod-resid-chip-fg",
  },
  VIDA: {
    nome: "Vida",
    icone: HeartPulse,
    amostra: "bg-prod-vida",
    chip: "bg-prod-vida-chip text-prod-vida-chip-fg",
  },
};

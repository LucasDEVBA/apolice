import {
  Calculator,
  Info,
  LayoutDashboard,
  LifeBuoy,
  ShieldCheck,
  SlidersHorizontal,
  type LucideIcon,
} from "lucide-react";

import type { Tom } from "@/ui/componentes/Badge";

import type { Papel } from "./personas";

export interface ItemNavegacao {
  para: string;
  rotulo: string;
  /** Rótulo da bottom nav (≤ 9 caracteres). */
  rotuloCurto?: string;
  icone: LucideIcon;
  /** NavLink "end": só ativo na rota exata (Dashboard). */
  exato?: boolean;
  /** Contador à direita (vem da API na fase 2). */
  contador?: number;
}

export const NAVEGACAO_PRINCIPAL: readonly ItemNavegacao[] = [
  { para: "/", rotulo: "Dashboard", rotuloCurto: "Início", icone: LayoutDashboard, exato: true },
  { para: "/cotacoes", rotulo: "Cotações", icone: Calculator },
  { para: "/apolices", rotulo: "Apólices", icone: ShieldCheck },
  { para: "/sinistros", rotulo: "Sinistros", icone: LifeBuoy },
];

export const ITEM_ADMIN: ItemNavegacao = {
  para: "/admin/tarifas",
  rotulo: "Admin",
  icone: SlidersHorizontal,
};
export const ITEM_SOBRE: ItemNavegacao = { para: "/sobre", rotulo: "Sobre o projeto", icone: Info };

export interface FilaSalva {
  rotulo: string;
  para: string;
  tom: Tom;
  /** Itens na fila (vem da API na fase 2). */
  contador?: number;
}

/** Filas de trabalho por papel, só com dados que o SPEC tem (DESIGN.md §4.2). */
export const FILAS_POR_PAPEL: Record<Papel, readonly FilaSalva[]> = {
  Corretor: [
    { rotulo: "Cotações expirando (48 h)", para: "/cotacoes?fila=expirando-48h", tom: "warn" },
    { rotulo: "Renovações em 30 dias", para: "/apolices?fila=renovacao-30d", tom: "info" },
    { rotulo: "Aceitas a emitir", para: "/cotacoes?status=Aceita", tom: "info" },
  ],
  Analista: [
    { rotulo: "Sinistros abertos", para: "/sinistros?status=Aberto", tom: "info" },
    { rotulo: "Em análise", para: "/sinistros?status=EmAnalise", tom: "warn" },
  ],
  Admin: [],
};

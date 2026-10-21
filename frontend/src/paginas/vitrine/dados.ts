/** Dados fictícios coerentes com o SPEC (somas fecham, alíquotas e regras reais) para a vitrine. */
import type { StatusApolice } from "@/ui/componentes/Badge";
import type { FatorCalculo, ItemCalculo } from "@/ui/componentes/PainelCalculo";
import type { CodigoProduto } from "@/ui/componentes/ProdutoTag";
import type { EventoTimeline } from "@/ui/componentes/Timeline";
import type { PontoMensal } from "@/ui/graficos/GraficoBarras";

export const HOJE = new Date("2026-10-21T14:36:08-03:00");

export interface ApoliceResumo {
  id: string;
  numero: string;
  produto: CodigoProduto;
  segurado: string;
  cpfMascarado: string;
  premioTotal: number;
  vigenciaInicio: string;
  vigenciaFim: string;
  status: StatusApolice;
}

export const APOLICES: readonly ApoliceResumo[] = [
  {
    id: "1",
    numero: "AUTO-2026-000042",
    produto: "AUTO",
    segurado: "Rafael Nogueira Castro",
    cpfMascarado: "***.482.917-**",
    premioTotal: 4862.38,
    vigenciaInicio: "2026-03-15",
    vigenciaFim: "2027-03-15",
    status: "Ativa",
  },
  {
    id: "2",
    numero: "RESID-2026-000017",
    produto: "RESID",
    segurado: "Patrícia Menezes",
    cpfMascarado: "***.115.630-**",
    premioTotal: 1184,
    vigenciaInicio: "2026-11-05",
    vigenciaFim: "2027-11-05",
    status: "Programada",
  },
  {
    id: "3",
    numero: "VIDA-2025-000063",
    produto: "VIDA",
    segurado: "Helena Duarte",
    cpfMascarado: "***.903.244-**",
    premioTotal: 2640,
    vigenciaInicio: "2025-09-01",
    vigenciaFim: "2026-09-01",
    status: "Vencida",
  },
  {
    id: "4",
    numero: "AUTO-2026-000051",
    produto: "AUTO",
    segurado: "Marcelo Tavares",
    cpfMascarado: "***.271.508-**",
    premioTotal: 3412.6,
    vigenciaInicio: "2026-10-21",
    vigenciaFim: "2027-10-21",
    status: "Ativa",
  },
  {
    id: "5",
    numero: "AUTO-2025-000981",
    produto: "AUTO",
    segurado: "Rodrigo Saldanha",
    cpfMascarado: "***.640.019-**",
    premioTotal: 4215.7,
    vigenciaInicio: "2025-11-09",
    vigenciaFim: "2026-11-09",
    status: "Cancelada",
  },
];

// Tarifação Auto: itens já com fatores (prêmio fixo não sofre fator); líquido = soma dos itens.
export const ITENS_AUTO: readonly ItemCalculo[] = [
  { codigo: "CASCO", nome: "Casco", premio: 2986.42 },
  { codigo: "RCF_DM", nome: "RCF danos materiais", premio: 442.04, alterado: true },
  { codigo: "RCF_DC", nome: "RCF danos corporais", premio: 255.95 },
  { codigo: "VIDROS", nome: "Vidros, faróis e retrovisores", premio: 240.42 },
  { codigo: "CARRO_RESERVA", nome: "Carro reserva", premio: 152, fixo: true },
  { codigo: "ASSISTENCIA", nome: "Assistência 24 h", premio: 98, fixo: true },
];

// Efeitos estimados aplicados em sequência; o último fecha o arredondamento (DESIGN.md §4.12).
export const FATORES_AUTO: readonly FatorCalculo[] = [
  {
    id: "idade",
    descricao: "Condutor de 18 a 25 anos",
    detalhe: "Condutor principal com 22 anos",
    fator: 1.4,
    aplicaSobre: "Casco, RCF-DM, RCF-DC e Vidros",
    efeito: 1209.21,
  },
  {
    id: "regiao",
    descricao: "Região 4 · BA/SE",
    detalhe: "CEP de pernoite 41820-021",
    fator: 1.12,
    efeito: 507.87,
  },
  { id: "garagem", descricao: "Garagem em casa e no trabalho", fator: 0.92, efeito: -379.21 },
  {
    id: "bonus",
    descricao: "Bônus classe 3",
    detalhe: "Três anos sem sinistro",
    fator: 0.9,
    efeito: -436.07,
  },
];

export const CALCULO_AUTO = {
  premioLiquido: 4174.83,
  aliquotaIof: 0.0738,
  iof: 308.1,
  premioTotal: 4482.93,
  maxParcelas: 10,
  validaAte: "2026-11-20",
  referencia: "2026-10-01",
} as const;

export const PREMIO_MENSAL: readonly PontoMensal[] = [
  { mes: "2025-11", valor: 4120.4, detalhe: "1 apólice" },
  { mes: "2025-12", valor: 0, detalhe: "nenhuma apólice" },
  { mes: "2026-01", valor: 3412.6, detalhe: "1 apólice" },
  { mes: "2026-02", valor: 5208.1, detalhe: "2 apólices" },
  { mes: "2026-03", valor: 4862.38, detalhe: "1 apólice" },
  { mes: "2026-04", valor: 2980, detalhe: "1 apólice" },
  { mes: "2026-05", valor: 6140.75, detalhe: "2 apólices" },
  { mes: "2026-06", valor: 3720.9, detalhe: "1 apólice" },
  { mes: "2026-07", valor: 4590.3, detalhe: "2 apólices" },
  { mes: "2026-08", valor: 5310, detalhe: "2 apólices" },
  { mes: "2026-09", valor: 4482.93, detalhe: "1 apólice" },
  { mes: "2026-10", valor: 2860.5, projecao: 4150, detalhe: "1 apólice" },
];

/** Média dos 11 meses fechados (out/2026 é parcial). */
export const MEDIA_MENSAL = 4075.31;

export const EVENTOS_APOLICE: readonly EventoTimeline[] = [
  {
    id: "parcela-9",
    quando: "2026-11-15",
    titulo: "Vencimento da parcela 9/10",
    descricao: "R$ 486,23 · próxima parcela",
    tom: "steel",
  },
  {
    id: "sinistro",
    quando: "2026-10-21T11:47:00-03:00",
    titulo: "Sinistro SIN-2026-000097 aberto",
    descricao: "Colisão parcial na Av. Tancredo Neves, Salvador/BA.",
    tom: "warn",
  },
  {
    id: "endosso",
    quando: "2026-06-12T15:40:00-03:00",
    titulo: "Endosso nº 1 emitido",
    descricao: "Inclusão de Vidros, faróis e retrovisores · ajuste de R$ 162,41.",
    tom: "gold",
  },
  {
    id: "boas-vindas",
    quando: "2026-03-15T09:13:00-03:00",
    titulo: "E-mail de boas-vindas enviado",
    descricao: "Caixa de saída simulada, com a apólice em PDF.",
    tom: "steel",
  },
  {
    id: "emissao",
    quando: "2026-03-15T09:12:00-03:00",
    titulo: "Apólice emitida",
    descricao: "A partir da cotação COT-2026-001187, por Ana Ribeiro.",
    tom: "navy",
  },
];

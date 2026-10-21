/** Personas de demonstração (SPEC §7.1, POST /auth/demo). Na fase 2 o login real substitui o estado local. */
export type Papel = "Corretor" | "Analista" | "Admin";
export type IdPersona = "corretora" | "corretor" | "analista" | "admin";

export interface Persona {
  id: IdPersona;
  nome: string;
  papel: Papel;
  /** Rótulo exibido ("Analista de sinistros"). */
  rotuloPapel: string;
  /** O que a persona enxerga, na voz do negócio. */
  escopo: string;
  avatar: "navy" | "steel" | "gold" | "marinho";
}

const ANA_RIBEIRO: Persona = {
  id: "corretora",
  nome: "Ana Ribeiro",
  papel: "Corretor",
  rotuloPapel: "Corretora",
  escopo: "Carteira completa, cotação e emissão de apólices",
  avatar: "navy",
};

export const PERSONAS: readonly Persona[] = [
  ANA_RIBEIRO,
  {
    id: "corretor",
    nome: "Bruno Lima",
    papel: "Corretor",
    rotuloPapel: "Corretor",
    escopo: "Somente a própria carteira, sem acesso ao Admin",
    avatar: "steel",
  },
  {
    id: "analista",
    nome: "Carla Souza",
    papel: "Analista",
    rotuloPapel: "Analista de sinistros",
    escopo: "Regulação e pagamento de sinistros",
    avatar: "gold",
  },
  {
    id: "admin",
    nome: "Diego Martins",
    papel: "Admin",
    rotuloPapel: "Admin",
    escopo: "Tabelas de tarifa e trilha de auditoria",
    avatar: "marinho",
  },
];

export const PERSONA_PADRAO: Persona = ANA_RIBEIRO;

export function buscarPersona(id: IdPersona): Persona {
  return PERSONAS.find((p) => p.id === id) ?? PERSONA_PADRAO;
}

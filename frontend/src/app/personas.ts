import type { Papel, UsuarioDto } from "@/api/contratos";

export type { Papel } from "@/api/contratos";

/** Personas de demonstração (SPEC §7.1, POST /auth/demo → Auth/PersonasDemo.cs). */
export type IdPersona = "corretora" | "corretor" | "analista" | "admin";

export interface Persona {
  /** null = usuário que entrou com e-mail e senha e não é uma persona de demonstração. */
  id: IdPersona | null;
  nome: string;
  email: string;
  papel: Papel;
  /** Rótulo exibido ("Analista de sinistros"). */
  rotuloPapel: string;
  /** O que a persona enxerga, na voz do negócio. */
  escopo: string;
  avatar: "navy" | "steel" | "gold" | "marinho";
}

export interface PersonaDemo extends Persona {
  id: IdPersona;
}

const ANA_RIBEIRO: PersonaDemo = {
  id: "corretora",
  nome: "Ana Ribeiro",
  email: "ana.corretora@apolice.dev",
  papel: "Corretor",
  rotuloPapel: "Corretora",
  escopo: "A própria carteira: cotação, emissão e pós-venda",
  avatar: "navy",
};

export const PERSONAS: readonly PersonaDemo[] = [
  ANA_RIBEIRO,
  {
    id: "corretor",
    nome: "Bruno Lima",
    email: "bruno.corretor@apolice.dev",
    papel: "Corretor",
    rotuloPapel: "Corretor",
    escopo: "A própria carteira, isolada da carteira da Ana",
    avatar: "steel",
  },
  {
    id: "analista",
    nome: "Carla Souza",
    email: "carla.analista@apolice.dev",
    papel: "Analista",
    rotuloPapel: "Analista de sinistros",
    escopo: "Regulação e pagamento de sinistros",
    avatar: "gold",
  },
  {
    id: "admin",
    nome: "Diego Martins",
    email: "admin@apolice.dev",
    papel: "Admin",
    rotuloPapel: "Admin",
    escopo: "Tabelas de tarifa e trilha de auditoria",
    avatar: "marinho",
  },
];

export const PERSONA_PADRAO: PersonaDemo = ANA_RIBEIRO;

export function buscarPersona(id: IdPersona): PersonaDemo {
  return PERSONAS.find((p) => p.id === id) ?? PERSONA_PADRAO;
}

const ROTULO_DO_PAPEL: Record<Papel, string> = {
  Corretor: "Corretor",
  Analista: "Analista de sinistros",
  Admin: "Admin",
};

const AVATAR_DO_PAPEL: Record<Papel, Persona["avatar"]> = {
  Corretor: "navy",
  Analista: "gold",
  Admin: "marinho",
};

/**
 * Como o usuário logado aparece no shell. Persona de demonstração pelo e-mail (o seed usa os
 * mesmos de PersonasDemo); qualquer outro usuário, pelo papel que a API devolveu.
 */
export function personaDoUsuario(usuario: UsuarioDto): Persona {
  const demo = PERSONAS.find((p) => p.email === usuario.email.toLowerCase());
  if (demo?.papel === usuario.papel) return { ...demo, nome: usuario.nome };
  return {
    id: null,
    nome: usuario.nome,
    email: usuario.email,
    papel: usuario.papel,
    rotuloPapel: ROTULO_DO_PAPEL[usuario.papel],
    escopo: "",
    avatar: AVATAR_DO_PAPEL[usuario.papel],
  };
}

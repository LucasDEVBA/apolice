import { createContext, use } from "react";

import type { IdPersona, Persona } from "./personas";

export interface ValorPersona {
  persona: Persona;
  trocarPersona: (id: IdPersona) => void;
}

export const PersonaContexto = createContext<ValorPersona | null>(null);

export function usePersona(): ValorPersona {
  const valor = use(PersonaContexto);
  if (!valor) throw new Error("usePersona precisa estar dentro de <PersonaProvider>.");
  return valor;
}

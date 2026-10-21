import { useState, type ReactNode } from "react";

import { PersonaContexto } from "./persona-contexto";
import { buscarPersona, PERSONA_PADRAO, type IdPersona, type Persona } from "./personas";

/** Persona ativa em memória (fase 1). Na fase 2 vem do /auth/me e o access token fica só em memória. */
export function PersonaProvider({
  children,
  inicial = PERSONA_PADRAO.id,
}: {
  children: ReactNode;
  inicial?: IdPersona;
}) {
  const [persona, setPersona] = useState<Persona>(() => buscarPersona(inicial));
  return (
    <PersonaContexto value={{ persona, trocarPersona: (id) => setPersona(buscarPersona(id)) }}>
      {children}
    </PersonaContexto>
  );
}

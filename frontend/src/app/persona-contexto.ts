import { personaDoUsuario, type IdPersona, type Persona } from "./personas";
import { useSessao, useUsuario } from "./sessao/sessao-contexto";

export interface ValorPersona {
  persona: Persona;
  trocarPersona: (id: IdPersona) => Promise<void>;
}

/** Como o usuário logado aparece no shell (nome, papel, avatar) e a troca de persona demo. */
export function usePersona(): ValorPersona {
  const usuario = useUsuario();
  const { trocarPersona } = useSessao();
  return {
    persona: personaDoUsuario(usuario),
    trocarPersona: async (id) => {
      await trocarPersona(id);
    },
  };
}

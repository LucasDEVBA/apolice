import { createContext, use } from "react";

import type { UsuarioDto } from "@/api/contratos";

import type { IdPersona } from "../personas";

export type EstadoSessao =
  /** Restaurando a sessão pelo cookie de refresh (carga da página). */
  | { status: "verificando" }
  | { status: "anonima"; motivo?: "expirada" | "saiu" }
  | { status: "autenticada"; usuario: UsuarioDto };

export interface ValorSessao {
  estado: EstadoSessao;
  entrar: (email: string, senha: string) => Promise<UsuarioDto>;
  entrarComoPersona: (persona: IdPersona) => Promise<UsuarioDto>;
  sair: () => Promise<void>;
  /** Sai da sessão atual (revoga o refresh) e entra como outra persona de demonstração. */
  trocarPersona: (persona: IdPersona) => Promise<UsuarioDto>;
}

export const SessaoContexto = createContext<ValorSessao | null>(null);

export function useSessao(): ValorSessao {
  const valor = use(SessaoContexto);
  if (!valor) throw new Error("useSessao precisa estar dentro de <SessaoProvider>.");
  return valor;
}

/** Usuário logado: só use abaixo de <RotaProtegida>, onde a sessão já está autenticada. */
export function useUsuario(): UsuarioDto {
  const { estado } = useSessao();
  if (estado.status !== "autenticada") {
    throw new Error("useUsuario só funciona dentro de uma rota protegida.");
  }
  return estado.usuario;
}

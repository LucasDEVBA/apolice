import type { AuthResponse, LoginDemoRequest, LoginRequest, UsuarioDto } from "../contratos";
import { CABECALHO_CSRF, requisitar } from "../http";

/** POST /auth/login — anônimo; devolve o access token e grava o cookie de refresh. */
export function login(corpo: LoginRequest): Promise<AuthResponse> {
  return requisitar("/auth/login", { metodo: "POST", corpo, anonima: true });
}

/** POST /auth/demo — anônimo, atrás da flag LoginDemo. */
export function loginDemo(corpo: LoginDemoRequest): Promise<AuthResponse> {
  return requisitar("/auth/demo", { metodo: "POST", corpo, anonima: true });
}

/** POST /auth/refresh — cookie HttpOnly + X-Requested-With; use renovarSessao() (single-flight). */
export function refresh(): Promise<AuthResponse> {
  return requisitar("/auth/refresh", { metodo: "POST", cabecalhos: CABECALHO_CSRF, anonima: true });
}

/** POST /auth/logout — revoga o refresh e limpa o cookie (204). */
export function logout(): Promise<void> {
  return requisitar("/auth/logout", {
    metodo: "POST",
    cabecalhos: CABECALHO_CSRF,
    anonima: true,
    resposta: "vazia",
  });
}

/** GET /auth/me */
export function obterUsuarioAtual(sinal?: AbortSignal): Promise<UsuarioDto> {
  return requisitar("/auth/me", { sinal });
}

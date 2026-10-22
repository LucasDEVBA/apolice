import type {
  AbrirSinistroRequest,
  AprovarSinistroRequest,
  ListarSinistrosQuery,
  NegarSinistroRequest,
  Pagina,
  SinistroDto,
  SinistroResumoDto,
} from "../contratos";
import { requisitar } from "../http";

const caminhoDo = (id: string) => `/sinistros/${encodeURIComponent(id)}`;

/** POST /sinistros → 201 (Idempotency-Key por submissão). */
export function abrirSinistro(
  corpo: AbrirSinistroRequest,
  chaveIdempotencia: string,
): Promise<SinistroDto> {
  return requisitar("/sinistros", { metodo: "POST", corpo, chaveIdempotencia });
}

/** GET /sinistros?status&busca&cursor&limite */
export function listarSinistros(
  query: ListarSinistrosQuery,
  sinal?: AbortSignal,
): Promise<Pagina<SinistroResumoDto>> {
  return requisitar("/sinistros", { query: { ...query }, sinal });
}

/** GET /sinistros/{id} */
export function obterSinistro(id: string, sinal?: AbortSignal): Promise<SinistroDto> {
  return requisitar(caminhoDo(id), { sinal });
}

/** POST /sinistros/{id}/analise — Analista. */
export function iniciarAnaliseSinistro(id: string): Promise<SinistroDto> {
  return requisitar(`${caminhoDo(id)}/analise`, { metodo: "POST" });
}

/** POST /sinistros/{id}/aprovacao — Analista. */
export function aprovarSinistro(id: string, corpo: AprovarSinistroRequest): Promise<SinistroDto> {
  return requisitar(`${caminhoDo(id)}/aprovacao`, { metodo: "POST", corpo });
}

/** POST /sinistros/{id}/negativa — Analista. */
export function negarSinistro(id: string, corpo: NegarSinistroRequest): Promise<SinistroDto> {
  return requisitar(`${caminhoDo(id)}/negativa`, { metodo: "POST", corpo });
}

/** POST /sinistros/{id}/pagamento — Analista. */
export function registrarPagamentoSinistro(id: string): Promise<SinistroDto> {
  return requisitar(`${caminhoDo(id)}/pagamento`, { metodo: "POST" });
}

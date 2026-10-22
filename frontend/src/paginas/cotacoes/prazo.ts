import type { CotacaoResumoDto } from "@/api/contratos";

const HORAS_ALERTA = 48;
const MS_POR_HORA = 3_600_000;
const MS_POR_DIA = 86_400_000;

/** Cotação aberta (calculada ou aceita) que vence nas próximas 48 h (fila do corretor). */
export function expiraEmBreve(
  cotacao: Pick<CotacaoResumoDto, "status" | "validaAte">,
  agora = Date.now(),
): boolean {
  if (cotacao.status !== "Calculada" && cotacao.status !== "Aceita") return false;
  const restante = new Date(cotacao.validaAte).getTime() - agora;
  return restante > 0 && restante <= HORAS_ALERTA * MS_POR_HORA;
}

/** "expira em 2 dias" / "expira em 5 h": chip de prazo sempre com o número (DESIGN.md §4.6). */
export function textoDoPrazo(validaAte: string, agora = Date.now()): string {
  const restante = new Date(validaAte).getTime() - agora;
  if (restante <= 0) return "expirada";
  if (restante < MS_POR_DIA) {
    const horas = Math.max(1, Math.round(restante / MS_POR_HORA));
    return `expira em ${String(horas)} h`;
  }
  const dias = Math.round(restante / MS_POR_DIA);
  return dias === 1 ? "expira em 1 dia" : `expira em ${String(dias)} dias`;
}

/**
 * Datas do mock no calendário de Brasília (SPEC §2: offset fixo −03:00, sem horário de verão).
 * DateOnly como "aaaa-mm-dd" e instantes como ISO com −03:00, o formato que a API serializa.
 */
import { diasEntre, hojeEmBrasilia, idadeEm, somarDias } from "@/ui/utils/datas";

const OFFSET = "-03:00";
const HORAS_OFFSET_BRASILIA = 3;

export { diasEntre, idadeEm, somarDias };
export const hoje = hojeEmBrasilia;

/** Mesmo dia N meses depois; 31/01 + 1 mês = 28/02 (ou 29), como o DateOnly.AddMonths do .NET. */
export function somarMeses(data: string, meses: number): string {
  const [ano, mes, dia] = data.split("-").map(Number) as [number, number, number];
  const alvo = new Date(Date.UTC(ano, mes - 1 + meses, 1));
  const ultimoDia = new Date(
    Date.UTC(alvo.getUTCFullYear(), alvo.getUTCMonth() + 1, 0),
  ).getUTCDate();
  alvo.setUTCDate(Math.min(dia, ultimoDia));
  return alvo.toISOString().slice(0, 10);
}

const MINUTOS_ANTES_DE_AGORA = 25;

/**
 * Instante ISO em Brasília: "2026-10-21T14:32:00-03:00". O seed usa horas fixas; se a hora de hoje
 * ainda não chegou, recua para alguns minutos atrás (nada de evento "em 3 h" no histórico).
 */
export function instante(data: string, hora = "10:00"): string {
  const valor = `${data}T${hora}:00${OFFSET}`;
  const agora = Date.now();
  return new Date(valor).getTime() <= agora
    ? valor
    : agoraIso(new Date(agora - MINUTOS_ANTES_DE_AGORA * 60_000));
}

/** Agora como instante ISO com −03:00. */
export function agoraIso(agora: Date = new Date()): string {
  const local = new Date(agora.getTime() - HORAS_OFFSET_BRASILIA * 3_600_000);
  return `${local.toISOString().slice(0, 19)}${OFFSET}`;
}

/** Data (Brasília) de um instante ISO. */
export function dataDoInstante(iso: string): string {
  return hoje(new Date(iso));
}

export function anoDe(data: string): number {
  return Number(data.slice(0, 4));
}

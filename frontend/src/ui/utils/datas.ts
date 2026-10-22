/**
 * Aritmética de DateOnly ("aaaa-mm-dd") no calendário de Brasília (UTC−3 fixo, SPEC §2). Conta
 * em UTC puro: nada de new Date("aaaa-mm-dd") no fuso do navegador.
 */
const MS_POR_DIA = 86_400_000;
const HORAS_OFFSET_BRASILIA = 3;

function paraUtc(data: string): number {
  const [ano, mes, dia] = data.split("-").map(Number) as [number, number, number];
  return Date.UTC(ano, mes - 1, dia);
}

function deUtc(ms: number): string {
  return new Date(ms).toISOString().slice(0, 10);
}

/** Hoje em Brasília, como DateOnly. */
export function hojeEmBrasilia(agora: Date = new Date()): string {
  return deUtc(agora.getTime() - HORAS_OFFSET_BRASILIA * 3_600_000);
}

export function somarDias(data: string, dias: number): string {
  return deUtc(paraUtc(data) + dias * MS_POR_DIA);
}

/** b − a, em dias. */
export function diasEntre(a: string, b: string): number {
  return Math.round((paraUtc(b) - paraUtc(a)) / MS_POR_DIA);
}

/** "2026-10" do dia informado. */
export function mesDe(data: string): string {
  return data.slice(0, 7);
}

/** Idade completa na data de referência. */
export function idadeEm(nascimento: string, referencia: string): number {
  const [an, mn, dn] = nascimento.split("-").map(Number) as [number, number, number];
  const [ar, mr, dr] = referencia.split("-").map(Number) as [number, number, number];
  const fezAniversario = mr > mn || (mr === mn && dr >= dn);
  return ar - an - (fezAniversario ? 0 : 1);
}

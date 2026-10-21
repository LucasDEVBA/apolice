/**
 * Formatação pt-BR única do frontend (DESIGN.md §3.8). Nenhum componente monta número, moeda
 * ou data à mão: passa por aqui, para a mesma regra valer em tabela, KPI e leitor de tela.
 */

const LOCALE = "pt-BR";
// Datas com hora são mostradas no fuso de Brasília, como o backend grava (SPEC §2).
const FUSO_BRASILIA = "America/Sao_Paulo";
const CASAS_PERCENTUAL = 2;
const CASAS_FATOR = 2;
const SEGUNDOS_POR_MINUTO = 60;
const SEGUNDOS_POR_HORA = 3_600;
const SEGUNDOS_POR_DIA = 86_400;
const DIAS_POR_MES = 30;
const DIAS_POR_ANO = 365;
const PADRAO_DATA_SOMENTE = /^(\d{4})-(\d{2})-(\d{2})$/;
const PADRAO_MES = /^(\d{4})-(\d{2})$/;
// Meia-noite em Brasília, em UTC: o Brasil não tem horário de verão desde 2019 (UTC−3 fixo).
const HORA_UTC_DA_MEIA_NOITE_EM_BRASILIA = 3;

const fmtMoeda = new Intl.NumberFormat(LOCALE, { style: "currency", currency: "BRL" });
const fmtCompacto = new Intl.NumberFormat(LOCALE, {
  notation: "compact",
  maximumFractionDigits: 1,
});
const fmtData = new Intl.DateTimeFormat(LOCALE, {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  timeZone: FUSO_BRASILIA,
});
const fmtHora = new Intl.DateTimeFormat(LOCALE, {
  hour: "2-digit",
  minute: "2-digit",
  timeZone: FUSO_BRASILIA,
});
const fmtHoraSegundos = new Intl.DateTimeFormat(LOCALE, {
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  timeZone: FUSO_BRASILIA,
});
const fmtRelativo = new Intl.RelativeTimeFormat(LOCALE, { numeric: "auto", style: "short" });
const fmtMesCurto = new Intl.DateTimeFormat(LOCALE, { month: "short", timeZone: "UTC" });
const fmtMesLongo = new Intl.DateTimeFormat(LOCALE, { month: "long", timeZone: "UTC" });

/** R$ 4.862,38 (o espaço depois de R$ é NBSP, U+00A0). */
export function formatarMoeda(valor: number): string {
  return fmtMoeda.format(valor);
}

/**
 * Lê um valor em reais digitado em pt-BR: "100000", "100.000" e "100.000,50" valem; vazio é
 * undefined; o que não é número (duas vírgulas) vira NaN, para a validação dizer o que fazer.
 */
export function lerMoedaDigitada(texto: string): number | undefined {
  const limpo = texto.trim();
  if (limpo === "") return undefined;
  if (!/^[\d.]*(,\d*)?$/.test(limpo) || !/\d/.test(limpo)) return Number.NaN;
  const valor = Number(limpo.replaceAll(".", "").replace(",", "."));
  return Number.isFinite(valor) ? valor : Number.NaN;
}

/** 1.284 · 31,6 (casas fixas). */
export function formatarNumero(valor: number, casas = 0): string {
  return new Intl.NumberFormat(LOCALE, {
    minimumFractionDigits: casas,
    maximumFractionDigits: casas,
  }).format(valor);
}

/** Recebe a fração (0,0738) e devolve 7,38%. A alíquota vem do DTO; nunca fixe 7,38%. */
export function formatarPercentual(fracao: number, casas = CASAS_PERCENTUAL): string {
  return new Intl.NumberFormat(LOCALE, {
    style: "percent",
    minimumFractionDigits: casas,
    maximumFractionDigits: casas,
  }).format(fracao);
}

export interface NumeroCompacto {
  /** "286,4" */
  numero: string;
  /** "mil" | "mi" | "" */
  sufixo: string;
  /** "286,4 mil" */
  texto: string;
}

/** 286.412,90 → 286,4 mil · 4.080.000 → 4,1 mi. */
export function formatarCompacto(valor: number): NumeroCompacto {
  const partes = fmtCompacto.formatToParts(valor);
  const sufixo = partes.find((p) => p.type === "compact")?.value ?? "";
  const numero = partes
    .filter((p) => p.type !== "compact" && p.type !== "literal")
    .map((p) => p.value)
    .join("");
  return { numero, sufixo, texto: sufixo ? `${numero} ${sufixo}` : numero };
}

export interface PartesMoeda {
  sinal: "" | "−";
  simbolo: string;
  /** "4.862" */
  inteiro: string;
  /** ",38" */
  centavos: string;
}

/** Separa a moeda para o número-herói (R$ sobrescrito, centavos reduzidos). */
export function partesMoeda(valor: number): PartesMoeda {
  const partes = fmtMoeda.formatToParts(Math.abs(valor));
  const juntar = (tipos: Intl.NumberFormatPartTypes[]) =>
    partes
      .filter((p) => tipos.includes(p.type))
      .map((p) => p.value)
      .join("");
  return {
    sinal: valor < 0 ? "−" : "",
    simbolo: juntar(["currency"]),
    inteiro: juntar(["integer", "group"]),
    centavos: juntar(["decimal", "fraction"]),
  };
}

/** Fator multiplicativo da tarifação: 1,4 → ×1,40. */
export function formatarFator(fator: number, casas = CASAS_FATOR): string {
  return `×${formatarNumero(fator, casas)}`;
}

export interface DataSomente {
  ano: number;
  mes: number;
  dia: number;
}

/**
 * Lê um DateOnly ("2026-03-15") sem passar por new Date(str): isso vira UTC e mostraria
 * 14/03 em Brasília.
 */
export function lerDataSomente(iso: string): DataSomente {
  const encontrado = PADRAO_DATA_SOMENTE.exec(iso);
  if (!encontrado) throw new RangeError(`Data inválida (esperado aaaa-mm-dd): "${iso}"`);
  const [, ano, mes, dia] = encontrado;
  return { ano: Number(ano), mes: Number(mes), dia: Number(dia) };
}

function ehDataSomente(valor: string): boolean {
  return PADRAO_DATA_SOMENTE.test(valor);
}

/**
 * DateOnly vira a meia-noite em Brasília (o dia certo em qualquer fuso do navegador); instante ISO
 * e Date passam direto. Nunca new Date("2026-03-15"), que é meia-noite UTC = 14/03 21:00 aqui.
 */
function paraDate(valor: string | Date): Date {
  if (typeof valor === "string" && ehDataSomente(valor)) {
    const { ano, mes, dia } = lerDataSomente(valor);
    return new Date(Date.UTC(ano, mes - 1, dia, HORA_UTC_DA_MEIA_NOITE_EM_BRASILIA));
  }
  const data = typeof valor === "string" ? new Date(valor) : valor;
  if (Number.isNaN(data.getTime())) throw new RangeError(`Data inválida: "${String(valor)}"`);
  return data;
}

const doisDigitos = (n: number) => String(n).padStart(2, "0");

/** 21/10/2026. Aceita DateOnly ("2026-10-21"), instante ISO ou Date. */
export function formatarData(valor: string | Date): string {
  if (typeof valor === "string" && ehDataSomente(valor)) {
    const { ano, mes, dia } = lerDataSomente(valor);
    return `${doisDigitos(dia)}/${doisDigitos(mes)}/${ano}`;
  }
  return fmtData.format(paraDate(valor));
}

/** 14:32 (ou 14:36:08 com segundos), no fuso de Brasília. DateOnly não tem hora: é erro. */
export function formatarHora(valor: string | Date, comSegundos = false): string {
  if (typeof valor === "string" && ehDataSomente(valor)) {
    throw new RangeError(`Data sem hora (DateOnly) não tem horário para exibir: "${valor}"`);
  }
  return (comSegundos ? fmtHoraSegundos : fmtHora).format(paraDate(valor));
}

/** 21/10/2026 · 14:32. Um DateOnly sai só com a data: inventar "00:00" seria mentir a hora. */
export function formatarDataHora(valor: string | Date): string {
  if (typeof valor === "string" && ehDataSomente(valor)) return formatarData(valor);
  return `${formatarData(paraDate(valor))} · ${formatarHora(valor)}`;
}

/**
 * "há 12 min", "ontem", "em 3 dias". A data absoluta deve ir no title de quem chama. DateOnly
 * conta a partir da meia-noite em Brasília.
 */
export function formatarRelativo(valor: string | Date, agora: Date = new Date()): string {
  const segundos = Math.round((paraDate(valor).getTime() - agora.getTime()) / 1000);
  const absoluto = Math.abs(segundos);
  const [quantidade, unidade] = escolherUnidade(segundos, absoluto);
  // O estilo curto do pt-BR termina abreviações com ponto ("min."); o guia usa sem.
  return fmtRelativo.format(quantidade, unidade).replace(/\.$/, "");
}

function escolherUnidade(
  segundos: number,
  absoluto: number,
): [number, Intl.RelativeTimeFormatUnit] {
  if (absoluto < SEGUNDOS_POR_MINUTO) return [0, "second"];
  if (absoluto < SEGUNDOS_POR_HORA) return [Math.trunc(segundos / SEGUNDOS_POR_MINUTO), "minute"];
  if (absoluto < SEGUNDOS_POR_DIA) return [Math.trunc(segundos / SEGUNDOS_POR_HORA), "hour"];
  const dias = Math.trunc(segundos / SEGUNDOS_POR_DIA);
  if (Math.abs(dias) < DIAS_POR_MES) return [dias, "day"];
  if (Math.abs(dias) < DIAS_POR_ANO) return [Math.trunc(dias / DIAS_POR_MES), "month"];
  return [Math.trunc(dias / DIAS_POR_ANO), "year"];
}

function lerMes(mes: string): { ano: number; mes: number } {
  const encontrado = PADRAO_MES.exec(mes);
  if (!encontrado) throw new RangeError(`Mês inválido (esperado aaaa-mm): "${mes}"`);
  return { ano: Number(encontrado[1]), mes: Number(encontrado[2]) };
}

function dataDoMes(mes: string): Date {
  const lido = lerMes(mes);
  return new Date(Date.UTC(lido.ano, lido.mes - 1, 1));
}

/** "2026-10" → "out" (eixo de gráfico). */
export function formatarMesEixo(mes: string): string {
  return fmtMesCurto.format(dataDoMes(mes)).replace(/\.$/, "");
}

/** "2026-09" → "set/2026" (tooltip). */
export function formatarMesAno(mes: string): string {
  return `${formatarMesEixo(mes)}/${lerMes(mes).ano}`;
}

/** "2026-10" → "outubro de 2026" (texto para leitor de tela e tabela). */
export function formatarMesPorExtenso(mes: string): string {
  return `${fmtMesLongo.format(dataDoMes(mes))} de ${lerMes(mes).ano}`;
}

export function anoDoMes(mes: string): number {
  return lerMes(mes).ano;
}

/** CPF sempre mascarado em listas: 123.482.917-00 → ***.482.917-** */
export function mascararCpf(cpf: string): string {
  const digitos = cpf.replace(/\D/g, "");
  if (digitos.length !== 11) return "***.***.***-**";
  return `***.${digitos.slice(3, 6)}.${digitos.slice(6, 9)}-**`;
}

/** "Ana Ribeiro" → "AR"; "Carla" → "CA". */
export function iniciais(nome: string): string {
  const palavras = nome.trim().split(/\s+/).filter(Boolean);
  const primeira = palavras[0] ?? "";
  if (palavras.length === 1) return primeira.slice(0, 2).toUpperCase();
  const ultima = palavras[palavras.length - 1] ?? "";
  return `${primeira.charAt(0)}${ultima.charAt(0)}`.toUpperCase();
}

const CENTAVOS_POR_REAL = 100;

export interface Parcelamento {
  quantidade: number;
  /** A 1ª parcela absorve o ajuste de centavos (SPEC §4.4). */
  primeira: number;
  demais: number;
}

/**
 * Mesmo cronograma do domínio: valor base = floor(total / N, 2 casas) e a 1ª parcela fica com a
 * diferença. Conta em centavos inteiros para não herdar erro de ponto flutuante.
 */
export function calcularParcelas(total: number, quantidade: number): Parcelamento {
  if (!Number.isInteger(quantidade) || quantidade < 1) {
    throw new RangeError(`Quantidade de parcelas inválida: ${quantidade}`);
  }
  const totalCentavos = Math.round(total * CENTAVOS_POR_REAL);
  const baseCentavos = Math.floor(totalCentavos / quantidade);
  const primeiraCentavos = totalCentavos - baseCentavos * (quantidade - 1);
  return {
    quantidade,
    primeira: primeiraCentavos / CENTAVOS_POR_REAL,
    demais: baseCentavos / CENTAVOS_POR_REAL,
  };
}

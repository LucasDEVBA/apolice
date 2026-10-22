/** Documentos brasileiros: CPF e CEP (validação e máscara), compartilhados pelo app e pelo mock. */

const TAMANHO_CPF = 11;
const TAMANHO_CEP = 8;

export function apenasDigitos(texto: string): string {
  return texto.replace(/\D/g, "");
}

/** 11 dígitos, sem sequência repetida e com os dois dígitos verificadores (SPEC §4.1). */
export function cpfValido(cpf: string): boolean {
  const d = apenasDigitos(cpf);
  if (d.length !== TAMANHO_CPF || /^(\d)\1{10}$/.test(d)) return false;
  const verificador = (n: number) => {
    let soma = 0;
    for (let i = 0; i < n; i++) soma += Number(d.charAt(i)) * (n + 1 - i);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };
  return verificador(9) === Number(d[9]) && verificador(10) === Number(d[10]);
}

/** Máscara progressiva 000.000.000-00 enquanto digita. */
export function mascararCpfDigitado(texto: string): string {
  const d = apenasDigitos(texto).slice(0, TAMANHO_CPF);
  return d
    .replace(/^(\d{3})(\d)/, "$1.$2")
    .replace(/^(\d{3})\.(\d{3})(\d)/, "$1.$2.$3")
    .replace(/\.(\d{3})(\d{1,2})$/, ".$1-$2");
}

export function cepValido(cep: string): boolean {
  return apenasDigitos(cep).length === TAMANHO_CEP;
}

/** Máscara 00000-000 enquanto digita. */
export function mascararCepDigitado(texto: string): string {
  const d = apenasDigitos(texto).slice(0, TAMANHO_CEP);
  return d.length > 5 ? `${d.slice(0, 5)}-${d.slice(5)}` : d;
}

/** "41820021" → "41820-021". */
export function formatarCep(cep: string): string {
  return mascararCepDigitado(cep);
}

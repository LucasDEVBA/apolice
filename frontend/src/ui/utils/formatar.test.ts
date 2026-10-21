import { describe, expect, it } from "vitest";

import {
  formatarCompacto,
  formatarData,
  formatarDataHora,
  formatarFator,
  formatarHora,
  formatarMesAno,
  formatarMesEixo,
  formatarMoeda,
  formatarNumero,
  formatarPercentual,
  formatarRelativo,
  iniciais,
  lerDataSomente,
  lerMoedaDigitada,
  mascararCpf,
  partesMoeda,
} from "./formatar";
import { calcularParcelas } from "./parcelas";

const NBSP = " ";

describe("formatarMoeda", () => {
  it("deve formatar reais com separador de milhar, vírgula decimal e NBSP depois de R$", () => {
    expect(formatarMoeda(1234.56)).toBe(`R$${NBSP}1.234,56`);
  });

  it("deve manter duas casas em valores redondos e em zero", () => {
    expect(formatarMoeda(100000)).toBe(`R$${NBSP}100.000,00`);
    expect(formatarMoeda(0)).toBe(`R$${NBSP}0,00`);
  });
});

describe("formatarPercentual", () => {
  it("deve receber a fração e mostrar 7,38%", () => {
    expect(formatarPercentual(0.0738)).toBe("7,38%");
  });

  it("deve mostrar o IOF de VIDA como 0,38%", () => {
    expect(formatarPercentual(0.0038)).toBe("0,38%");
  });

  it("deve respeitar a quantidade de casas pedida", () => {
    expect(formatarPercentual(0.542, 1)).toBe("54,2%");
  });
});

describe("formatarData", () => {
  it("deve formatar DateOnly sem deslocar o dia pelo fuso", () => {
    expect(formatarData("2026-10-21")).toBe("21/10/2026");
    expect(formatarData("2026-03-01")).toBe("01/03/2026");
  });

  it("deve formatar instantes no fuso de Brasília", () => {
    expect(formatarData("2026-10-22T01:30:00Z")).toBe("21/10/2026");
  });

  it("deve recusar texto que não é data", () => {
    expect(() => formatarData("ontem")).toThrow(RangeError);
    expect(() => lerDataSomente("21/10/2026")).toThrow(RangeError);
  });
});

describe("formatarDataHora", () => {
  it("deve juntar data e hora com ponto médio", () => {
    expect(formatarDataHora("2026-10-21T14:32:00-03:00")).toBe("21/10/2026 · 14:32");
  });

  it("deve mostrar só a data de um DateOnly, sem inventar hora nem voltar um dia", () => {
    // new Date("2026-03-15") seria meia-noite UTC = 14/03 21:00 em Brasília.
    expect(formatarDataHora("2026-03-15")).toBe("15/03/2026");
  });
});

describe("formatarHora", () => {
  it("deve recusar DateOnly, que não tem horário", () => {
    expect(() => formatarHora("2026-03-15")).toThrow(RangeError);
  });
});

describe("formatarRelativo", () => {
  const agora = new Date("2026-10-21T14:44:00-03:00");

  it("deve mostrar minutos sem o ponto de abreviação", () => {
    expect(formatarRelativo("2026-10-21T14:32:00-03:00", agora)).toBe("há 12 min");
  });

  it("deve usar ontem para um dia atrás", () => {
    expect(formatarRelativo("2026-10-20T14:00:00-03:00", agora)).toBe("ontem");
  });

  it("deve tratar o futuro", () => {
    expect(formatarRelativo("2026-10-24T14:44:00-03:00", agora)).toBe("em 3 dias");
  });

  it("deve contar DateOnly a partir da meia-noite em Brasília", () => {
    const meiaNoite = new Date("2026-10-21T00:00:00-03:00");
    expect(formatarRelativo("2026-10-24", meiaNoite)).toBe("em 3 dias");
    expect(formatarRelativo("2026-10-20", meiaNoite)).toBe("ontem");
  });
});

describe("lerMoedaDigitada", () => {
  it("deve aceitar o valor como a pessoa digita em pt-BR", () => {
    expect(lerMoedaDigitada("100000")).toBe(100000);
    expect(lerMoedaDigitada("100.000")).toBe(100000);
    expect(lerMoedaDigitada("100.000,50")).toBe(100000.5);
    expect(lerMoedaDigitada(" 1,5 ")).toBe(1.5);
  });

  it("deve devolver undefined no vazio e NaN no que não é número", () => {
    expect(lerMoedaDigitada("")).toBeUndefined();
    expect(lerMoedaDigitada("1,2,3")).toBeNaN();
    expect(lerMoedaDigitada(".")).toBeNaN();
    expect(lerMoedaDigitada("abc")).toBeNaN();
  });
});

describe("números", () => {
  it("deve agrupar milhares", () => {
    expect(formatarNumero(1284)).toBe("1.284");
    expect(formatarNumero(31.6, 1)).toBe("31,6");
  });

  it("deve abreviar em mil e mi", () => {
    expect(formatarCompacto(286412.9)).toEqual({
      numero: "286,4",
      sufixo: "mil",
      texto: "286,4 mil",
    });
    expect(formatarCompacto(4_080_000).texto).toBe("4,1 mi");
    expect(formatarCompacto(57)).toEqual({ numero: "57", sufixo: "", texto: "57" });
  });

  it("deve separar a moeda em partes para o número-herói", () => {
    expect(partesMoeda(4862.38)).toEqual({
      sinal: "",
      simbolo: "R$",
      inteiro: "4.862",
      centavos: ",38",
    });
    expect(partesMoeda(-12.5).sinal).toBe("−");
  });

  it("deve formatar fatores com sinal de multiplicação", () => {
    expect(formatarFator(1.4)).toBe("×1,40");
    expect(formatarFator(0.91)).toBe("×0,91");
  });
});

describe("meses", () => {
  it("deve abreviar o mês do eixo sem ponto", () => {
    expect(formatarMesEixo("2026-10")).toBe("out");
    expect(formatarMesEixo("2026-01")).toBe("jan");
    expect(formatarMesAno("2026-09")).toBe("set/2026");
  });
});

describe("identificação", () => {
  it("deve mascarar o CPF", () => {
    expect(mascararCpf("123.482.917-00")).toBe("***.482.917-**");
    expect(mascararCpf("123")).toBe("***.***.***-**");
  });

  it("deve tirar as iniciais do primeiro e do último nome", () => {
    expect(iniciais("Ana Ribeiro")).toBe("AR");
    expect(iniciais("Rafael Nogueira Castro")).toBe("RC");
    expect(iniciais("Carla")).toBe("CA");
  });
});

describe("calcularParcelas", () => {
  it("deve dar a diferença de centavos para a 1ª parcela", () => {
    expect(calcularParcelas(4862.38, 10)).toEqual({
      quantidade: 10,
      primeira: 486.31,
      demais: 486.23,
    });
  });

  it("deve fechar a soma exatamente no total", () => {
    const { primeira, demais, quantidade } = calcularParcelas(4581.08, 10);
    expect(Math.round((primeira + demais * (quantidade - 1)) * 100)).toBe(458108);
  });

  it("deve recusar quantidade inválida", () => {
    expect(() => calcularParcelas(100, 0)).toThrow(RangeError);
  });
});

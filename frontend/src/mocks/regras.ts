/**
 * Regras do domínio (SPEC §4.2–4.5) reproduzidas para o mock: os números da simulação, do endosso,
 * do cancelamento e da triagem saem das mesmas fórmulas e tabelas do backend.
 */
import type {
  CoberturaApoliceDto,
  CoberturaRequest,
  DadosRisco,
  FatorDto,
  ItemTarifadoDto,
  MetodoCancelamento,
  MotivoCancelamento,
  ParcelaDto,
  ProdutoDto,
  SeguradoRequest,
  SimulacaoCancelamentoDto,
  TarifacaoDto,
} from "@/api/contratos";

import { anoDe, diasEntre, idadeEm, somarMeses } from "./datas";

export class ErroRegra extends Error {
  readonly codigo: string;
  readonly tipo: "Validacao" | "RegraDeNegocio";

  constructor(
    codigo: string,
    mensagem: string,
    tipo: "Validacao" | "RegraDeNegocio" = "Validacao",
  ) {
    super(mensagem);
    this.codigo = codigo;
    this.tipo = tipo;
  }
}

const PARCELA_MINIMA = 60;
const MAX_PARCELAS = 10;
const PORMIL = 1000;

/** Arredondamento do domínio: 2 casas, MidpointRounding.AwayFromZero. */
export function arred(valor: number): number {
  const sinal = valor < 0 ? -1 : 1;
  return (sinal * Math.round(Math.abs(valor) * 100 + 1e-9)) / 100;
}

const fmtMoeda = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
export const moeda = (valor: number) => fmtMoeda.format(valor);

interface FatorTarifario {
  descricao: string;
  fator: number;
  /** null = todo item com taxa. */
  alvos: readonly string[] | null;
}

function cepValido(cep: string): boolean {
  return /^\d{8}$/.test(cep);
}

function exigir(condicao: boolean, codigo: string, mensagem: string): void {
  if (!condicao) throw new ErroRegra(codigo, mensagem);
}

const FAIXAS_CONDUTOR = [
  { min: 18, max: 25, fator: 1.4 },
  { min: 26, max: 35, fator: 1.1 },
  { min: 36, max: 60, fator: 1.0 },
  { min: 61, max: 90, fator: 1.15 },
] as const;

const REGIOES: Record<string, { regiao: string; fator: number }> = {
  "0": { regiao: "Grande São Paulo", fator: 1.25 },
  "2": { regiao: "RJ/ES", fator: 1.3 },
  "4": { regiao: "BA/SE", fator: 1.1 },
};

const FAIXAS_VIDA = [
  { min: 18, max: 30, fator: 1.0 },
  { min: 31, max: 40, fator: 1.5 },
  { min: 41, max: 50, fator: 3.0 },
  { min: 51, max: 60, fator: 6.25 },
  { min: 61, max: 70, fator: 12.5 },
] as const;

function fatoresAuto(
  dados: Extract<DadosRisco, { produto: "AUTO" }>,
  ref: string,
): FatorTarifario[] {
  const ano = anoDe(ref);
  exigir(
    dados.valorVeiculo >= 10_000 && dados.valorVeiculo <= 500_000,
    "Tarifacao.ValorVeiculoForaDoLimite",
    `O valor do veículo deve estar entre ${moeda(10_000)} e ${moeda(500_000)}.`,
  );
  exigir(
    dados.anoFabricacao >= ano - 20 && dados.anoFabricacao <= ano + 1,
    "Tarifacao.AnoFabricacaoInvalido",
    `O ano de fabricação deve estar entre ${String(ano - 20)} e ${String(ano + 1)}.`,
  );
  exigir(cepValido(dados.cepPernoite), "Tarifacao.CepInvalido", "O CEP deve ter 8 dígitos.");
  exigir(
    dados.idadeCondutor >= 18 && dados.idadeCondutor <= 90,
    "Tarifacao.IdadeCondutorForaDoLimite",
    "A idade do condutor deve estar entre 18 e 90 anos.",
  );
  exigir(
    dados.classeBonus >= 0 && dados.classeBonus <= 10,
    "Tarifacao.ClasseBonusInvalida",
    "A classe de bônus deve estar entre 0 e 10.",
  );
  const faixa = FAIXAS_CONDUTOR.find(
    (f) => dados.idadeCondutor >= f.min && dados.idadeCondutor <= f.max,
  );
  const regiao = REGIOES[dados.cepPernoite.charAt(0)];
  const fatores: FatorTarifario[] = [
    {
      descricao: `Idade do condutor: ${String(faixa?.min ?? 18)} a ${String(faixa?.max ?? 90)} anos`,
      fator: faixa?.fator ?? 1,
      alvos: null,
    },
    regiao
      ? { descricao: `Região de pernoite: ${regiao.regiao}`, fator: regiao.fator, alvos: null }
      : { descricao: "Região de pernoite: demais regiões", fator: 1, alvos: null },
    { descricao: "Uso comercial", fator: dados.usoComercial ? 1.35 : 1, alvos: null },
  ];
  if (ano - dados.anoFabricacao > 10) {
    fatores.push({ descricao: "Veículo com mais de 10 anos", fator: 1.1, alvos: null });
  }
  fatores.push({
    descricao: `Bônus classe ${String(dados.classeBonus)}`,
    fator: arred4(1 - 0.03 * dados.classeBonus),
    alvos: null,
  });
  return fatores;
}

function arred4(valor: number): number {
  return Math.round(valor * 10_000) / 10_000;
}

function fatoresResid(dados: Extract<DadosRisco, { produto: "RESID" }>): FatorTarifario[] {
  exigir(
    dados.valorImovel >= 50_000 && dados.valorImovel <= 5_000_000,
    "Tarifacao.ValorImovelForaDoLimite",
    `O valor do imóvel deve estar entre ${moeda(50_000)} e ${moeda(5_000_000)}.`,
  );
  exigir(cepValido(dados.cep), "Tarifacao.CepInvalido", "O CEP deve ter 8 dígitos.");
  const fatores: FatorTarifario[] = [
    dados.tipoImovel === "Apartamento"
      ? { descricao: "Apartamento", fator: 0.8, alvos: null }
      : { descricao: "Casa (roubo)", fator: 1.3, alvos: ["ROUBO"] },
  ];
  if (!dados.habitual) {
    fatores.push({ descricao: "Imóvel não habitual (veraneio)", fator: 1.3, alvos: null });
  }
  return fatores;
}

function fatoresVida(
  dados: Extract<DadosRisco, { produto: "VIDA" }>,
  segurado: SeguradoRequest,
  ref: string,
): FatorTarifario[] {
  const idade = idadeEm(segurado.dataNascimento, ref);
  exigir(
    idade >= 18 && idade <= 70,
    "Tarifacao.IdadeSeguradoForaDoLimite",
    "A idade do segurado deve estar entre 18 e 70 anos.",
  );
  const faixa = FAIXAS_VIDA.find((f) => idade >= f.min && idade <= f.max);
  const fatores: FatorTarifario[] = [
    {
      descricao: `Faixa etária: ${String(faixa?.min ?? 18)} a ${String(faixa?.max ?? 70)} anos (morte)`,
      fator: faixa?.fator ?? 1,
      alvos: ["MORTE"],
    },
  ];
  if (dados.fumante) fatores.push({ descricao: "Fumante (morte)", fator: 1.5, alvos: ["MORTE"] });
  return fatores;
}

function definirFatores(
  produto: ProdutoDto,
  dados: DadosRisco,
  segurado: SeguradoRequest,
  ref: string,
): FatorTarifario[] {
  exigir(
    dados.produto === produto.codigo,
    "Tarifacao.ProdutoIncompativel",
    "Os dados de risco não correspondem ao produto informado.",
  );
  switch (dados.produto) {
    case "AUTO":
      return fatoresAuto(dados, ref);
    case "RESID":
      return fatoresResid(dados);
    case "VIDA":
      return fatoresVida(dados, segurado, ref);
  }
}

function lmiDoRisco(codigo: string, dados: DadosRisco): number | null {
  return dados.produto === "AUTO" && codigo === "CASCO" ? dados.valorVeiculo : null;
}

function resolverLmis(
  produto: ProdutoDto,
  dados: DadosRisco,
  solicitadas: readonly CoberturaRequest[],
): Map<string, number> {
  const codigos = new Set<string>();
  for (const s of solicitadas) {
    const codigo = s.codigo.trim().toUpperCase();
    exigir(
      produto.coberturas.some((c) => c.codigo === codigo),
      "Tarifacao.CoberturaInexistente",
      `A cobertura '${codigo}' não existe no produto.`,
    );
    exigir(
      !codigos.has(codigo),
      "Tarifacao.CoberturaDuplicada",
      `A cobertura '${codigo}' foi informada mais de uma vez.`,
    );
    codigos.add(codigo);
  }
  for (const c of produto.coberturas.filter((c) => c.obrigatoria)) {
    exigir(
      codigos.has(c.codigo),
      "Tarifacao.CoberturaObrigatoriaAusente",
      `A cobertura obrigatória '${c.codigo}' não foi informada.`,
    );
  }
  const lmis = new Map<string, number>();
  for (const cobertura of produto.coberturas.filter((c) => codigos.has(c.codigo))) {
    const pedido = solicitadas.find((s) => s.codigo.trim().toUpperCase() === cobertura.codigo);
    const lmi = cobertura.lmiFixo ?? lmiDoRisco(cobertura.codigo, dados) ?? pedido?.lmi ?? null;
    exigir(
      lmi !== null,
      "Tarifacao.LmiObrigatorio",
      `Informe o LMI da cobertura '${cobertura.codigo}'.`,
    );
    const valor = lmi ?? 0;
    exigir(
      valor >= cobertura.lmiMinimo && valor <= cobertura.lmiMaximo,
      "Tarifacao.LmiForaDoLimite",
      `O LMI da cobertura '${cobertura.codigo}' deve estar entre ${moeda(cobertura.lmiMinimo)} e ${moeda(cobertura.lmiMaximo)}.`,
    );
    lmis.set(cobertura.codigo, valor);
  }
  if (dados.produto === "RESID") {
    exigir(
      (lmis.get("INCENDIO") ?? 0) <= dados.valorImovel,
      "Tarifacao.LmiAcimaDoValorImovel",
      "O LMI de incêndio não pode superar o valor do imóvel.",
    );
  }
  const ipa = lmis.get("IPA");
  if (ipa !== undefined) {
    exigir(
      ipa <= (lmis.get("MORTE") ?? 0),
      "Tarifacao.LmiIpaAcimaDaMorte",
      "O LMI de invalidez não pode superar o LMI de morte.",
    );
  }
  return lmis;
}

/** ITarifador.Calcular — SPEC §4.2. */
export function tarifar(
  produto: ProdutoDto,
  segurado: SeguradoRequest,
  dados: DadosRisco,
  solicitadas: readonly CoberturaRequest[],
  dataReferencia: string,
): TarifacaoDto {
  const fatores = definirFatores(produto, dados, segurado, dataReferencia);
  const lmis = resolverLmis(produto, dados, solicitadas);
  const contratadas = produto.coberturas.filter((c) => lmis.has(c.codigo));
  const comTaxa = contratadas.filter((c) => c.tipoCalculo !== "PremioFixo").map((c) => c.codigo);
  const aplicaA = (f: FatorTarifario, codigo: string) =>
    f.alvos === null || f.alvos.includes(codigo);
  const efetivos = fatores.filter(
    (f) => f.fator !== 1 && comTaxa.some((codigo) => aplicaA(f, codigo)),
  );

  const itens: ItemTarifadoDto[] = contratadas.map((c) => {
    const lmi = lmis.get(c.codigo) ?? 0;
    const multiplicador = efetivos
      .filter((f) => aplicaA(f, c.codigo))
      .reduce((p, f) => p * f.fator, 1);
    const premio =
      c.tipoCalculo === "TaxaSobreLmi"
        ? arred(lmi * c.taxa * multiplicador)
        : c.tipoCalculo === "TaxaPorMil"
          ? arred(((lmi * c.taxa) / PORMIL) * multiplicador)
          : (c.premioFixo ?? 0);
    return {
      codigo: c.codigo,
      nome: c.nome,
      lmi,
      premioLiquido: premio,
      obrigatoria: c.obrigatoria,
      permiteSinistro: c.permiteSinistro,
    };
  });
  const premioLiquido = arred(itens.reduce((s, i) => s + i.premioLiquido, 0));
  const iof = arred(premioLiquido * produto.aliquotaIof);
  const premioTotal = arred(premioLiquido + iof);
  const fatoresDto: FatorDto[] = efetivos.map((f) => ({ descricao: f.descricao, fator: f.fator }));
  return {
    itens,
    fatores: fatoresDto,
    premioLiquido,
    aliquotaIof: produto.aliquotaIof,
    iof,
    premioTotal,
    maxParcelas: Math.min(MAX_PARCELAS, Math.max(1, Math.floor(premioTotal / PARCELA_MINIMA))),
  };
}

/** CronogramaParcelas — base = floor(total/N, 2 casas); a 1ª absorve a diferença. */
export function cronograma(premioTotal: number, parcelas: number, inicio: string): ParcelaDto[] {
  const totalCentavos = Math.round(premioTotal * 100);
  const base = Math.floor(totalCentavos / parcelas);
  const primeira = totalCentavos - base * (parcelas - 1);
  return Array.from({ length: parcelas }, (_, i) => ({
    numero: i + 1,
    vencimento: somarMeses(inicio, i),
    valor: (i === 0 ? primeira : base) / 100,
  }));
}

const PRAZO_CURTO: readonly (readonly [number, number])[] = [
  [15, 13],
  [30, 20],
  [45, 27],
  [60, 30],
  [75, 37],
  [90, 40],
  [105, 46],
  [120, 50],
  [135, 56],
  [150, 60],
  [165, 66],
  [180, 70],
  [195, 73],
  [210, 75],
  [225, 78],
  [240, 80],
  [255, 83],
  [270, 85],
  [285, 88],
  [300, 90],
  [315, 93],
  [330, 95],
  [345, 98],
  [365, 100],
  [366, 100],
];

/** SimularCancelamento — SPEC §4.4 (IOF não é restituído). */
export function simularCancelamento(
  base: number,
  inicio: string,
  fim: string,
  motivo: MotivoCancelamento,
  hojeData: string,
): SimulacaoCancelamentoDto {
  const diasVigencia = diasEntre(inicio, fim);
  const comum = { dataCancelamento: hojeData, diasVigencia, premioLiquidoBase: base };
  if (hojeData < inicio) {
    return {
      ...comum,
      diasDecorridos: 0,
      metodo: "Integral",
      percentualRetido: null,
      valorRestituicao: base,
    };
  }
  const diasDecorridos = diasEntre(inicio, hojeData);
  if (motivo === "PedidoDoSegurado") {
    const linha = PRAZO_CURTO.find(([dias]) => dias >= diasDecorridos) ?? [366, 100];
    const retido = linha[1] / 100;
    const metodo: MetodoCancelamento = "PrazoCurto";
    return {
      ...comum,
      diasDecorridos,
      metodo,
      percentualRetido: retido,
      valorRestituicao: arred(base * (1 - retido)),
    };
  }
  const restantes = Math.max(0, diasEntre(hojeData, fim));
  return {
    ...comum,
    diasDecorridos,
    metodo: "ProRataDie",
    percentualRetido: null,
    valorRestituicao: arred((base * restantes) / diasVigencia),
  };
}

/** Ajuste pro rata do endosso — SPEC §4.4. */
export function ajusteEndosso(
  anualAnterior: number,
  anualNovo: number,
  diasRestantes: number,
  diasTotais: number,
  aliquotaIof: number,
): { ajusteLiquido: number; iofAjuste: number; ajusteTotal: number } {
  const ajusteLiquido = arred(((anualNovo - anualAnterior) * diasRestantes) / diasTotais);
  const iofAjuste = arred(ajusteLiquido * aliquotaIof);
  return { ajusteLiquido, iofAjuste, ajusteTotal: arred(ajusteLiquido + iofAjuste) };
}

/** AvaliadorTriagem — SPEC §4.5. */
export function avaliarTriagem(
  dataOcorrencia: string,
  inicioVigencia: string,
  valorReclamado: number,
  lmi: number,
  abertoEmData: string,
): { score: number; alertas: string[] } {
  const alertas: string[] = [];
  let score = 0;
  if (diasEntre(inicioVigencia, dataOcorrencia) <= 30) {
    score += 40;
    alertas.push("VIGENCIA_RECENTE");
  }
  if (valorReclamado > lmi * 0.8) {
    score += 30;
    alertas.push("VALOR_PROXIMO_LMI");
  }
  if (valorReclamado > lmi) {
    score += 20;
    alertas.push("VALOR_ACIMA_LMI");
  }
  if (diasEntre(dataOcorrencia, abertoEmData) > 30) {
    score += 10;
    alertas.push("COMUNICACAO_TARDIA");
  }
  return { score: Math.min(100, score), alertas };
}

export function coberturasDaTarifacao(t: TarifacaoDto): CoberturaApoliceDto[] {
  return t.itens.map((i) => ({ ...i }));
}

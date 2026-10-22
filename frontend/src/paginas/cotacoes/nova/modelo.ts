import { z } from "zod";

import type {
  CodigoProduto,
  CoberturaProdutoDto,
  CotacaoRequest,
  DadosRisco,
  ProdutoDto,
  TipoImovel,
} from "@/api/contratos";
import { apenasDigitos, cepValido, cpfValido } from "@/ui/utils/documentos";
import { idadeEm } from "@/ui/utils/datas";
import { formatarMoeda } from "@/ui/utils/formatar";

/* ---------------------------------------------------------------- faixas do SPEC §4.2 */

export const FAIXAS = {
  valorVeiculo: { min: 10_000, max: 500_000 },
  anosRetroativos: 20,
  anosFuturos: 1,
  idadeCondutor: { min: 18, max: 90 },
  classeBonus: { min: 0, max: 10 },
  valorImovel: { min: 50_000, max: 5_000_000 },
  idadeVida: { min: 18, max: 70 },
  nome: { min: 3, max: 120 },
} as const;

/** No SPEC o LMI do Casco é sempre o valor do veículo (sem escolha de % FIPE). */
export const COBERTURA_LMI_DO_VEICULO = "CASCO";

/** Sugestões de LMI ao ligar uma cobertura (ficam dentro da faixa do catálogo). */
const LMI_SUGERIDO: Record<string, number> = {
  RCF_DM: 100_000,
  RCF_DC: 100_000,
  DANOS_ELETRICOS: 10_000,
  ROUBO: 20_000,
  RC_FAMILIAR: 50_000,
  VENDAVAL: 20_000,
  MORTE: 100_000,
  IPA: 50_000,
};

/* ---------------------------------------------------------------- formulário */

export interface CoberturaFormulario {
  ativa: boolean;
  lmi?: number | undefined;
}

export interface FormularioCotacao {
  produto: CodigoProduto | "";
  segurado: { nome: string; cpf: string; email: string; dataNascimento: string };
  auto: {
    valorVeiculo?: number | undefined;
    anoFabricacao: string;
    cepPernoite: string;
    idadeCondutor: string;
    usoComercial: boolean;
    classeBonus: string;
  };
  resid: {
    valorImovel?: number | undefined;
    tipoImovel: TipoImovel;
    cep: string;
    habitual: boolean;
  };
  vida: { fumante: boolean };
  coberturas: Record<string, CoberturaFormulario>;
}

export function valoresIniciais(): FormularioCotacao {
  return {
    produto: "",
    segurado: { nome: "", cpf: "", email: "", dataNascimento: "" },
    auto: {
      anoFabricacao: "",
      cepPernoite: "",
      idadeCondutor: "",
      usoComercial: false,
      classeBonus: "0",
    },
    resid: { tipoImovel: "Apartamento", cep: "", habitual: true },
    vida: { fumante: false },
    coberturas: {},
  };
}

export function lmiVariavel(cobertura: CoberturaProdutoDto, produto: CodigoProduto): boolean {
  if (cobertura.lmiFixo !== null) return false;
  return !(produto === "AUTO" && cobertura.codigo === COBERTURA_LMI_DO_VEICULO);
}

/** Coberturas do produto escolhido: obrigatórias ligadas, demais desligadas com LMI sugerido. */
export function coberturasPadrao(produto: ProdutoDto): Record<string, CoberturaFormulario> {
  return Object.fromEntries(
    produto.coberturas.map((c) => {
      const sugerido = LMI_SUGERIDO[c.codigo];
      const lmi =
        lmiVariavel(c, produto.codigo) && sugerido !== undefined
          ? Math.min(c.lmiMaximo, Math.max(c.lmiMinimo, sugerido))
          : undefined;
      return [c.codigo, { ativa: c.obrigatoria, lmi }];
    }),
  );
}

/* ---------------------------------------------------------------- validação */

type Contexto = z.RefinementCtx;

function inteiro(texto: string): number | null {
  if (!/^\d+$/.test(texto.trim())) return null;
  return Number(texto.trim());
}

function exigirFaixa(
  ctx: Contexto,
  caminho: (string | number)[],
  valor: number | null | undefined,
  faixa: { min: number; max: number },
  mensagem: string,
) {
  if (
    valor === null ||
    valor === undefined ||
    Number.isNaN(valor) ||
    valor < faixa.min ||
    valor > faixa.max
  ) {
    ctx.addIssue({ code: "custom", path: caminho, message: mensagem });
  }
}

function validarAuto(v: FormularioCotacao, ctx: Contexto, anoAtual: number) {
  const { auto } = v;
  exigirFaixa(
    ctx,
    ["auto", "valorVeiculo"],
    auto.valorVeiculo,
    FAIXAS.valorVeiculo,
    `Informe um valor entre ${formatarMoeda(FAIXAS.valorVeiculo.min)} e ${formatarMoeda(FAIXAS.valorVeiculo.max)}.`,
  );
  const anoMin = anoAtual - FAIXAS.anosRetroativos;
  const anoMax = anoAtual + FAIXAS.anosFuturos;
  exigirFaixa(
    ctx,
    ["auto", "anoFabricacao"],
    inteiro(auto.anoFabricacao),
    { min: anoMin, max: anoMax },
    `Informe um ano entre ${String(anoMin)} e ${String(anoMax)}.`,
  );
  if (!cepValido(auto.cepPernoite)) {
    ctx.addIssue({
      code: "custom",
      path: ["auto", "cepPernoite"],
      message: "Informe o CEP com 8 dígitos.",
    });
  }
  exigirFaixa(
    ctx,
    ["auto", "idadeCondutor"],
    inteiro(auto.idadeCondutor),
    FAIXAS.idadeCondutor,
    "Informe uma idade entre 18 e 90 anos.",
  );
  exigirFaixa(
    ctx,
    ["auto", "classeBonus"],
    inteiro(auto.classeBonus),
    FAIXAS.classeBonus,
    "Escolha uma classe de bônus de 0 a 10.",
  );
}

function validarResid(v: FormularioCotacao, ctx: Contexto) {
  exigirFaixa(
    ctx,
    ["resid", "valorImovel"],
    v.resid.valorImovel,
    FAIXAS.valorImovel,
    `Informe um valor entre ${formatarMoeda(FAIXAS.valorImovel.min)} e ${formatarMoeda(FAIXAS.valorImovel.max)}.`,
  );
  if (!cepValido(v.resid.cep)) {
    ctx.addIssue({
      code: "custom",
      path: ["resid", "cep"],
      message: "Informe o CEP com 8 dígitos.",
    });
  }
}

function validarVida(v: FormularioCotacao, ctx: Contexto, hoje: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(v.segurado.dataNascimento)) return;
  const idade = idadeEm(v.segurado.dataNascimento, hoje);
  if (idade < FAIXAS.idadeVida.min || idade > FAIXAS.idadeVida.max) {
    ctx.addIssue({
      code: "custom",
      path: ["segurado", "dataNascimento"],
      message: `O seguro de vida aceita segurados de 18 a 70 anos (idade informada: ${String(idade)}).`,
    });
  }
}

/** Regras explicadas no campo (DESIGN.md §4.8): faixa do catálogo, IPA ≤ Morte, Incêndio ≤ imóvel. */
function validarCoberturas(v: FormularioCotacao, ctx: Contexto, produto: ProdutoDto) {
  const lmiDe = (codigo: string) => v.coberturas[codigo]?.lmi;
  for (const c of produto.coberturas) {
    const estado = v.coberturas[c.codigo];
    const ativa = c.obrigatoria || estado?.ativa === true;
    if (!ativa || !lmiVariavel(c, produto.codigo)) continue;
    const caminho = ["coberturas", c.codigo, "lmi"];
    exigirFaixa(
      ctx,
      caminho,
      estado?.lmi,
      { min: c.lmiMinimo, max: c.lmiMaximo },
      `Informe um LMI entre ${formatarMoeda(c.lmiMinimo)} e ${formatarMoeda(c.lmiMaximo)}.`,
    );
    const lmi = estado?.lmi ?? 0;
    if (c.codigo === "IPA" && lmi > (lmiDe("MORTE") ?? 0)) {
      ctx.addIssue({
        code: "custom",
        path: caminho,
        message: `IPA não pode passar o LMI de Morte (${formatarMoeda(lmiDe("MORTE") ?? 0)}).`,
      });
    }
    if (c.codigo === "INCENDIO" && v.resid.valorImovel !== undefined && lmi > v.resid.valorImovel) {
      ctx.addIssue({
        code: "custom",
        path: caminho,
        message: `Incêndio não pode passar o valor do imóvel (${formatarMoeda(v.resid.valorImovel)}).`,
      });
    }
  }
}

const PRODUTOS_VALIDOS: readonly string[] = ["AUTO", "RESID", "VIDA"];

/** Esquema do assistente: as faixas do domínio (SPEC §4.2) checadas antes de ir à API. */
export function criarEsquema(
  produtos: readonly ProdutoDto[],
  hoje: string,
): z.ZodType<FormularioCotacao, FormularioCotacao> {
  const anoAtual = Number(hoje.slice(0, 4));
  const esquema = z
    .object({
      produto: z
        .string()
        .refine((p) => PRODUTOS_VALIDOS.includes(p), "Escolha um produto para continuar."),
      segurado: z.object({
        nome: z
          .string()
          .trim()
          .min(FAIXAS.nome.min, "Informe o nome completo do segurado.")
          .max(FAIXAS.nome.max, "Use no máximo 120 caracteres."),
        cpf: z
          .string()
          .refine((cpf) => apenasDigitos(cpf).length > 0, "Informe o CPF do segurado.")
          .refine((cpf) => apenasDigitos(cpf).length === 0 || cpfValido(cpf), {
            message: "CPF inválido: confira os 11 dígitos.",
          }),
        email: z
          .string()
          .trim()
          .pipe(z.email("Informe um e-mail válido, como nome@exemplo.com.br.")),
        dataNascimento: z
          .string()
          .regex(/^\d{4}-\d{2}-\d{2}$/, "Informe a data de nascimento.")
          .refine((d) => d <= hoje, "A data de nascimento não pode ser futura."),
      }),
      auto: z.object({
        valorVeiculo: z.number().optional(),
        anoFabricacao: z.string(),
        cepPernoite: z.string(),
        idadeCondutor: z.string(),
        usoComercial: z.boolean(),
        classeBonus: z.string(),
      }),
      resid: z.object({
        valorImovel: z.number().optional(),
        tipoImovel: z.enum(["Casa", "Apartamento"]),
        cep: z.string(),
        habitual: z.boolean(),
      }),
      vida: z.object({ fumante: z.boolean() }),
      coberturas: z.record(
        z.string(),
        z.object({ ativa: z.boolean(), lmi: z.number().optional() }),
      ),
    })
    .superRefine((valores, ctx) => {
      const v = valores as FormularioCotacao;
      const produto = produtos.find((p) => p.codigo === v.produto);
      if (!produto) return;
      if (produto.codigo === "AUTO") validarAuto(v, ctx, anoAtual);
      if (produto.codigo === "RESID") validarResid(v, ctx);
      if (produto.codigo === "VIDA") validarVida(v, ctx, hoje);
      validarCoberturas(v, ctx, produto);
    });
  // O formulário guarda o produto como "" até a escolha; o esquema valida isso no refine acima.
  return esquema as unknown as z.ZodType<FormularioCotacao, FormularioCotacao>;
}

/* ---------------------------------------------------------------- pedido */

function dadosRisco(v: FormularioCotacao, produto: CodigoProduto): DadosRisco {
  switch (produto) {
    case "AUTO":
      return {
        produto: "AUTO",
        valorVeiculo: v.auto.valorVeiculo ?? 0,
        anoFabricacao: Number(v.auto.anoFabricacao),
        cepPernoite: apenasDigitos(v.auto.cepPernoite),
        idadeCondutor: Number(v.auto.idadeCondutor),
        usoComercial: v.auto.usoComercial,
        classeBonus: Number(v.auto.classeBonus),
      };
    case "RESID":
      return {
        produto: "RESID",
        valorImovel: v.resid.valorImovel ?? 0,
        tipoImovel: v.resid.tipoImovel,
        cep: apenasDigitos(v.resid.cep),
        habitual: v.resid.habitual,
      };
    case "VIDA":
      return { produto: "VIDA", fumante: v.vida.fumante };
  }
}

/** CotacaoRequest a partir do formulário (CPF e CEP só com dígitos; LMI nulo quando é fixo). */
export function paraPedido(v: FormularioCotacao, produto: ProdutoDto): CotacaoRequest {
  return {
    produtoCodigo: produto.codigo,
    segurado: {
      nome: v.segurado.nome.trim(),
      cpf: apenasDigitos(v.segurado.cpf),
      email: v.segurado.email.trim(),
      dataNascimento: v.segurado.dataNascimento,
    },
    dadosRisco: dadosRisco(v, produto.codigo),
    coberturas: produto.coberturas
      .filter((c) => c.obrigatoria || v.coberturas[c.codigo]?.ativa === true)
      .map((c) => ({
        codigo: c.codigo,
        lmi: lmiVariavel(c, produto.codigo) ? (v.coberturas[c.codigo]?.lmi ?? null) : null,
      })),
  };
}

/** Erro por campo da API → campo do formulário (dadosRisco depende do produto). */
export function mapaDeCamposDaApi(produto: CodigoProduto | ""): Record<string, string> {
  const grupo = produto === "AUTO" ? "auto" : produto === "RESID" ? "resid" : "vida";
  return {
    "segurado.nome": "segurado.nome",
    "segurado.cpf": "segurado.cpf",
    "segurado.email": "segurado.email",
    "segurado.dataNascimento": "segurado.dataNascimento",
    "dadosRisco.valorVeiculo": `${grupo}.valorVeiculo`,
    "dadosRisco.anoFabricacao": `${grupo}.anoFabricacao`,
    "dadosRisco.cepPernoite": `${grupo}.cepPernoite`,
    "dadosRisco.idadeCondutor": `${grupo}.idadeCondutor`,
    "dadosRisco.valorImovel": `${grupo}.valorImovel`,
    "dadosRisco.cep": `${grupo}.cep`,
  };
}

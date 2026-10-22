import type { CoberturaProdutoDto, ProdutoDto } from "@/api/contratos";

const IOF_PADRAO = 0.0738;
const IOF_VIDA = 0.0038;

function taxa(
  codigo: string,
  nome: string,
  descricao: string,
  obrigatoria: boolean,
  tipoCalculo: "TaxaSobreLmi" | "TaxaPorMil",
  valor: number,
  lmiMinimo: number,
  lmiMaximo: number,
): CoberturaProdutoDto {
  return {
    codigo,
    nome,
    descricao,
    obrigatoria,
    tipoCalculo,
    taxa: valor,
    premioFixo: null,
    lmiMinimo,
    lmiMaximo,
    lmiFixo: null,
    permiteSinistro: true,
  };
}

function fixo(
  codigo: string,
  nome: string,
  descricao: string,
  premio: number,
  lmiFixo: number,
  permiteSinistro: boolean,
): CoberturaProdutoDto {
  return {
    codigo,
    nome,
    descricao,
    obrigatoria: false,
    tipoCalculo: "PremioFixo",
    taxa: 0,
    premioFixo: premio,
    lmiMinimo: lmiFixo,
    lmiMaximo: lmiFixo,
    lmiFixo,
    permiteSinistro,
  };
}

/** Catálogo do SPEC §4.2 (o DataSeeder da API cria exatamente estes). */
export function criarCatalogo(): ProdutoDto[] {
  return [
    {
      codigo: "AUTO",
      nome: "Seguro Auto",
      descricao: "Proteção do veículo e responsabilidade civil perante terceiros.",
      aliquotaIof: IOF_PADRAO,
      coberturas: [
        {
          ...taxa(
            "CASCO",
            "Colisão, incêndio e roubo",
            "Danos ao próprio veículo; o LMI segue o valor do veículo.",
            true,
            "TaxaSobreLmi",
            0.032,
            10_000,
            500_000,
          ),
        },
        taxa(
          "RCF_DM",
          "RC danos materiais a terceiros",
          "Danos causados a veículos e bens de terceiros.",
          true,
          "TaxaSobreLmi",
          0.006,
          50_000,
          500_000,
        ),
        taxa(
          "RCF_DC",
          "RC danos corporais a terceiros",
          "Lesões e despesas médicas de terceiros.",
          false,
          "TaxaSobreLmi",
          0.004,
          50_000,
          500_000,
        ),
        fixo(
          "VIDROS",
          "Vidros, faróis e retrovisores",
          "Reparo ou troca em rede referenciada.",
          220,
          3_000,
          true,
        ),
        fixo(
          "CARRO_RESERVA",
          "Carro reserva (15 dias)",
          "Categoria básica com ar-condicionado.",
          180,
          0,
          false,
        ),
        fixo(
          "ASSISTENCIA_24H",
          "Assistência 24h",
          "Reboque, chaveiro, pane seca e troca de pneu.",
          120,
          0,
          false,
        ),
      ],
    },
    {
      codigo: "RESID",
      nome: "Seguro Residencial",
      descricao: "Proteção do imóvel e da família contra incêndio, roubo e danos.",
      aliquotaIof: IOF_PADRAO,
      coberturas: [
        taxa(
          "INCENDIO",
          "Incêndio, raio e explosão",
          "Cobertura básica do imóvel; até o valor do imóvel.",
          true,
          "TaxaSobreLmi",
          0.00045,
          10_000,
          5_000_000,
        ),
        taxa(
          "DANOS_ELETRICOS",
          "Danos elétricos",
          "Curto-circuito e oscilação de energia em equipamentos.",
          false,
          "TaxaSobreLmi",
          0.012,
          5_000,
          50_000,
        ),
        taxa(
          "ROUBO",
          "Roubo e furto qualificado",
          "Bens dentro do imóvel, com vestígio de arrombamento.",
          false,
          "TaxaSobreLmi",
          0.018,
          5_000,
          100_000,
        ),
        taxa(
          "RC_FAMILIAR",
          "Responsabilidade civil familiar",
          "Danos causados a terceiros pela família.",
          false,
          "TaxaSobreLmi",
          0.0035,
          10_000,
          200_000,
        ),
        taxa(
          "VENDAVAL",
          "Vendaval e granizo",
          "Danos ao imóvel por vendaval, furacão e granizo.",
          false,
          "TaxaSobreLmi",
          0.006,
          5_000,
          200_000,
        ),
      ],
    },
    {
      codigo: "VIDA",
      nome: "Seguro de Vida",
      descricao: "Proteção financeira para a família em caso de morte ou invalidez.",
      aliquotaIof: IOF_VIDA,
      coberturas: [
        taxa(
          "MORTE",
          "Morte natural ou acidental",
          "Indenização aos beneficiários.",
          true,
          "TaxaPorMil",
          0.8,
          20_000,
          2_000_000,
        ),
        taxa(
          "IPA",
          "Invalidez permanente por acidente",
          "Até o LMI de morte.",
          false,
          "TaxaPorMil",
          0.35,
          10_000,
          2_000_000,
        ),
        fixo(
          "FUNERAL",
          "Assistência funeral",
          "Organização e custeio do funeral.",
          90,
          7_000,
          true,
        ),
      ],
    },
  ];
}

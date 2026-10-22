import type { CotacaoDto, ProdutoDto } from "@/api/contratos";
import { mascararCepDigitado, mascararCpfDigitado } from "@/ui/utils/documentos";

import { coberturasPadrao, valoresIniciais, type FormularioCotacao } from "./nova/modelo";
import { salvarRascunho } from "./nova/rascunho";

const ETAPA_COBERTURAS = 3;

/** "Recalcular" uma cotação expirada: o assistente abre com os mesmos dados, na etapa Coberturas. */
export function prepararRecalculo(
  cotacao: CotacaoDto,
  produto: ProdutoDto,
  usuarioId: string,
): void {
  const base = valoresIniciais();
  const valores: FormularioCotacao = {
    ...base,
    produto: cotacao.produtoCodigo,
    segurado: {
      nome: cotacao.segurado.nome,
      cpf: mascararCpfDigitado(cotacao.segurado.cpf),
      email: cotacao.segurado.email,
      dataNascimento: cotacao.segurado.dataNascimento,
    },
    coberturas: {
      ...Object.fromEntries(
        Object.entries(coberturasPadrao(produto)).map(([c, e]) => [c, { ...e, ativa: false }]),
      ),
      ...Object.fromEntries(cotacao.itens.map((i) => [i.codigo, { ativa: true, lmi: i.lmi }])),
    },
  };
  const risco = cotacao.dadosRisco;
  if (risco.produto === "AUTO") {
    valores.auto = {
      valorVeiculo: risco.valorVeiculo,
      anoFabricacao: String(risco.anoFabricacao),
      cepPernoite: mascararCepDigitado(risco.cepPernoite),
      idadeCondutor: String(risco.idadeCondutor),
      usoComercial: risco.usoComercial,
      classeBonus: String(risco.classeBonus),
    };
  } else if (risco.produto === "RESID") {
    valores.resid = {
      valorImovel: risco.valorImovel,
      tipoImovel: risco.tipoImovel,
      cep: mascararCepDigitado(risco.cep),
      habitual: risco.habitual,
    };
  } else {
    valores.vida = { fumante: risco.fumante };
  }
  salvarRascunho(usuarioId, {
    valores,
    etapaMaxima: ETAPA_COBERTURAS + 1,
    salvoEm: new Date().toISOString(),
  });
}

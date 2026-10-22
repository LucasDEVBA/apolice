import type { FatorDto, ProdutoDto, TarifacaoDto } from "@/api/contratos";
import type { FatorCalculo, ItemCalculo } from "@/ui/componentes/PainelCalculo";

/**
 * Sobre quais itens o fator incide (SPEC §4.2): "(morte)" e "(roubo)" miram uma cobertura; os
 * demais multiplicam todos os itens com taxa. Prêmio fixo nunca recebe fator.
 */
export function aplicaSobre(fator: FatorDto, tarifacao: TarifacaoDto, produto: ProdutoDto): string {
  const alvo = /\((morte|roubo)\)$/i.exec(fator.descricao)?.[1]?.toUpperCase();
  const comTaxa = tarifacao.itens.filter((item) => {
    const tipo = produto.coberturas.find((c) => c.codigo === item.codigo)?.tipoCalculo;
    return tipo !== "PremioFixo" && (alvo === undefined || item.codigo === alvo);
  });
  const nomes = comTaxa.map((i) => i.nome);
  if (nomes.length <= 1) return nomes[0] ?? "";
  return `${nomes.slice(0, -1).join(", ")} e ${nomes.at(-1) ?? ""}`;
}

export function itensDoCalculo(
  tarifacao: TarifacaoDto,
  produto: ProdutoDto,
  anterior: TarifacaoDto | undefined,
): ItemCalculo[] {
  return tarifacao.itens.map((item) => {
    const antes = anterior?.itens.find((i) => i.codigo === item.codigo)?.premioLiquido;
    return {
      codigo: item.codigo,
      nome: item.nome,
      premio: item.premioLiquido,
      fixo: produto.coberturas.find((c) => c.codigo === item.codigo)?.tipoCalculo === "PremioFixo",
      alterado: anterior !== undefined && antes !== item.premioLiquido,
    };
  });
}

export function fatoresDoCalculo(tarifacao: TarifacaoDto, produto: ProdutoDto): FatorCalculo[] {
  return tarifacao.fatores.map((f, i) => ({
    id: `${String(i)}-${f.descricao}`,
    descricao: f.descricao,
    fator: f.fator,
    aplicaSobre: aplicaSobre(f, tarifacao, produto),
  }));
}

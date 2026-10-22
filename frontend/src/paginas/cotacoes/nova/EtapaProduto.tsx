import { Controller, useFormContext } from "react-hook-form";

import type { CodigoProduto, ProdutoDto } from "@/api/contratos";
import { GrupoCartoesEscolha } from "@/ui/componentes/CartaoEscolha";
import { formatarPercentual } from "@/ui/utils/formatar";

import { coberturasPadrao, type FormularioCotacao } from "./modelo";

/** Etapa 1: escolha do produto em cartões (DESIGN.md §4.10), a partir do catálogo da API. */
function resumoCoberturas(produto: ProdutoDto): string {
  const total = produto.coberturas.length;
  const obrigatorias = produto.coberturas.filter((c) => c.obrigatoria).length;
  const textoTotal = total === 1 ? "1 cobertura" : `${String(total)} coberturas`;
  const textoObrigatorias =
    obrigatorias === 1 ? "1 obrigatória" : `${String(obrigatorias)} obrigatórias`;
  return `${textoTotal} · ${textoObrigatorias}`;
}

export function EtapaProduto({ produtos }: { produtos: readonly ProdutoDto[] }) {
  const { control, setValue, formState } = useFormContext<FormularioCotacao>();
  const opcoes = produtos.map((p) => ({
    valor: p.codigo,
    titulo: p.nome,
    descricao: p.descricao,
    efeito: resumoCoberturas(p),
    fator: `IOF ${formatarPercentual(p.aliquotaIof)}`,
  }));
  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-3 type-label text-ink">Qual seguro você vai cotar?</legend>
      <Controller
        control={control}
        name="produto"
        render={({ field }) => (
          <GrupoCartoesEscolha<CodigoProduto>
            rotulo="Produto"
            opcoes={opcoes}
            valor={field.value}
            colunas={3}
            aoMudar={(codigo) => {
              const produto = produtos.find((p) => p.codigo === codigo);
              field.onChange(codigo);
              // Trocar de produto recomeça as coberturas pelo catálogo do novo produto.
              if (produto) setValue("coberturas", coberturasPadrao(produto));
            }}
          />
        )}
      />
      {formState.errors.produto?.message && (
        <p role="alert" className="type-meta text-bad">
          {formState.errors.produto.message}
        </p>
      )}
    </fieldset>
  );
}

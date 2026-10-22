import { useId } from "react";
import { Controller, useFormContext, useWatch } from "react-hook-form";

import type { CoberturaProdutoDto, ProdutoDto, TarifacaoDto } from "@/api/contratos";
import { FormField } from "@/ui/componentes/FormField";
import { InputMoeda } from "@/ui/componentes/InputMoeda";
import { SeloObrigatoria, Switch } from "@/ui/componentes/Switch";
import { Money } from "@/ui/componentes/Valores";
import { cn } from "@/ui/utils/cn";
import { formatarMoeda } from "@/ui/utils/formatar";

import {
  COBERTURA_LMI_DO_VEICULO,
  lmiVariavel,
  type CoberturaFormulario,
  type FormularioCotacao,
} from "./modelo";

interface LinhaProps {
  cobertura: CoberturaProdutoDto;
  produto: ProdutoDto;
  premio: number | undefined;
}

/** LMI que não se escolhe: fixo do catálogo ou o valor do veículo (Casco). */
function LmiNaoEditavel({
  cobertura,
  produto,
}: {
  cobertura: CoberturaProdutoDto;
  produto: ProdutoDto;
}) {
  const valorVeiculo = useWatch<FormularioCotacao, "auto.valorVeiculo">({
    name: "auto.valorVeiculo",
  });
  if (produto.codigo === "AUTO" && cobertura.codigo === COBERTURA_LMI_DO_VEICULO) {
    return (
      <p className="text-sm text-ink-2">
        Segue o valor do veículo
        {valorVeiculo !== undefined && (
          <span className="block font-semibold text-ink tnum">{formatarMoeda(valorVeiculo)}</span>
        )}
      </p>
    );
  }
  if (cobertura.lmiFixo === 0 || cobertura.lmiFixo === null) {
    return <p className="text-sm text-ink-2">Serviço, sem LMI</p>;
  }
  return (
    <p className="text-sm text-ink-2">
      LMI fixo{" "}
      <span className="block font-semibold text-ink tnum">{formatarMoeda(cobertura.lmiFixo)}</span>
    </p>
  );
}

function regraDoCampo(cobertura: CoberturaProdutoDto): string {
  const faixa = `Entre ${formatarMoeda(cobertura.lmiMinimo)} e ${formatarMoeda(cobertura.lmiMaximo)}`;
  if (cobertura.codigo === "IPA") return `${faixa}; não pode passar o LMI de Morte.`;
  if (cobertura.codigo === "INCENDIO") return `${faixa}; até o valor do imóvel.`;
  return `${faixa}.`;
}

function PrecoDaLinha({
  cobertura,
  ativa,
  premio,
}: {
  cobertura: CoberturaProdutoDto;
  ativa: boolean;
  premio: number | undefined;
}) {
  if (ativa) {
    return premio === undefined ? (
      <span className="text-sm text-ink-3">calculando…</span>
    ) : (
      <Money valor={premio} className="text-campo font-semibold text-ink" />
    );
  }
  if (cobertura.premioFixo !== null) {
    return (
      <span className="text-right text-sm text-ink-3 tnum">
        + {formatarMoeda(cobertura.premioFixo)}
        <span className="block text-xs">se ativada</span>
      </span>
    );
  }
  return <span className="text-right text-xs text-ink-3">Ative para calcular</span>;
}

function LinhaCobertura({ cobertura, produto, premio }: LinhaProps) {
  const id = useId();
  const {
    control,
    formState: { errors },
  } = useFormContext<FormularioCotacao>();
  // Um rascunho antigo pode não ter a cobertura: o tipo diz que existe, o runtime pode não ter.
  const estado = useWatch({ control, name: `coberturas.${cobertura.codigo}` }) as
    CoberturaFormulario | undefined;
  const ativa = cobertura.obrigatoria || estado?.ativa === true;
  const editavel = lmiVariavel(cobertura, produto.codigo);
  const erro = errors.coberturas?.[cobertura.codigo]?.lmi?.message;
  return (
    <li className="grid grid-cols-[auto_1fr] items-start gap-x-4 gap-y-3 py-4 md:grid-cols-[auto_minmax(0,1fr)_220px_120px] md:items-center">
      <Controller
        control={control}
        name={`coberturas.${cobertura.codigo}.ativa`}
        render={({ field }) => (
          <Switch
            aria-labelledby={`${id}-nome`}
            aria-describedby={`${id}-descricao`}
            ligado={field.value}
            aoMudar={field.onChange}
            obrigatorio={cobertura.obrigatoria}
            className="mt-0.5 md:mt-0"
          />
        )}
      />
      <div className="min-w-0">
        <p
          id={`${id}-nome`}
          className={cn(
            "flex flex-wrap items-center gap-2 text-sm font-semibold",
            ativa ? "text-ink" : "text-ink-2",
          )}
        >
          {cobertura.nome}
          {cobertura.obrigatoria && <SeloObrigatoria />}
        </p>
        <p id={`${id}-descricao`} className="type-meta text-ink-3">
          {cobertura.descricao}
          {!cobertura.permiteSinistro && " Não permite abertura de sinistro."}
          <span className="sr-only"> Código {cobertura.codigo}.</span>
        </p>
      </div>
      <div className="col-start-2 md:col-start-auto">
        {editavel ? (
          <FormField
            rotulo={`LMI de ${cobertura.nome}`}
            erro={ativa ? erro : undefined}
            ajuda={regraDoCampo(cobertura)}
            className="[&>div:first-child]:sr-only"
          >
            {(campo) => (
              <Controller
                control={control}
                name={`coberturas.${cobertura.codigo}.lmi`}
                render={({ field }) => (
                  <InputMoeda
                    {...campo}
                    tamanho="sm"
                    prefixo="LMI R$"
                    valor={field.value}
                    aoMudar={field.onChange}
                    onBlur={field.onBlur}
                    disabled={!ativa}
                  />
                )}
              />
            )}
          </FormField>
        ) : (
          <LmiNaoEditavel cobertura={cobertura} produto={produto} />
        )}
      </div>
      <div className="col-start-2 flex justify-start md:col-start-auto md:justify-end">
        <PrecoDaLinha cobertura={cobertura} ativa={ativa} premio={premio} />
      </div>
    </li>
  );
}

/** Etapa 4: coberturas do catálogo com toggle, LMI e prêmio do item vindo da simulação. */
export function EtapaCoberturas({
  produto,
  tarifacao,
}: {
  produto: ProdutoDto;
  tarifacao: TarifacaoDto | undefined;
}) {
  const coberturas = useWatch<FormularioCotacao, "coberturas">({ name: "coberturas" });
  const ativas = produto.coberturas.filter(
    (c) => c.obrigatoria || coberturas[c.codigo]?.ativa,
  ).length;
  const premioDe = (codigo: string) =>
    tarifacao?.itens.find((i) => i.codigo === codigo)?.premioLiquido;
  return (
    <div>
      <p className="flex items-center justify-between gap-3 border-b border-line pb-2.5 text-meta font-semibold text-ink-2">
        <span>Cobertura</span>
        <span
          className="rounded-full bg-neutral-bg px-2.5 py-0.5 text-neutral tnum"
          aria-live="polite"
        >
          {ativas} de {produto.coberturas.length} ativas
        </span>
      </p>
      <ul className="divide-y divide-line-soft">
        {produto.coberturas.map((c) => (
          <LinhaCobertura
            key={c.codigo}
            cobertura={c}
            produto={produto}
            premio={premioDe(c.codigo)}
          />
        ))}
      </ul>
    </div>
  );
}

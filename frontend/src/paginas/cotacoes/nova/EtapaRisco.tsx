import { Controller, useFormContext, useWatch } from "react-hook-form";

import { GrupoCartoesEscolha } from "@/ui/componentes/CartaoEscolha";
import { FormField } from "@/ui/componentes/FormField";
import { Input } from "@/ui/componentes/Input";
import { InputMoeda } from "@/ui/componentes/InputMoeda";
import { SegmentedControl } from "@/ui/componentes/SegmentedControl";
import { Select } from "@/ui/componentes/Select";
import { hojeEmBrasilia, idadeEm } from "@/ui/utils/datas";
import { mascararCepDigitado } from "@/ui/utils/documentos";
import { formatarMoeda, formatarPercentual } from "@/ui/utils/formatar";

import { FAIXAS, type FormularioCotacao } from "./modelo";

const CLASSES_BONUS = Array.from({ length: FAIXAS.classeBonus.max + 1 }, (_, i) => ({
  valor: String(i),
  rotulo:
    i === 0
      ? "Classe 0 · sem bônus"
      : `Classe ${String(i)} · desconto de ${formatarPercentual(0.03 * i, 0)}`,
}));

function CampoCep({
  nome,
  rotulo,
  ajuda,
}: {
  nome: "auto.cepPernoite" | "resid.cep";
  rotulo: string;
  ajuda: string;
}) {
  const { control, getFieldState, formState } = useFormContext<FormularioCotacao>();
  const erro = getFieldState(nome, formState).error?.message;
  return (
    <FormField rotulo={rotulo} erro={erro} obrigatorio ajuda={ajuda}>
      {(campo) => (
        <Controller
          control={control}
          name={nome}
          render={({ field }) => (
            <Input
              {...campo}
              ref={field.ref}
              name={field.name}
              value={field.value}
              onBlur={field.onBlur}
              onChange={(e) => field.onChange(mascararCepDigitado(e.target.value))}
              inputMode="numeric"
              autoComplete="postal-code"
              placeholder="00000-000"
            />
          )}
        />
      )}
    </FormField>
  );
}

function RiscoAuto() {
  const {
    control,
    register,
    formState: { errors },
  } = useFormContext<FormularioCotacao>();
  const ano = Number(hojeEmBrasilia().slice(0, 4));
  return (
    <div className="grid gap-5 md:grid-cols-2">
      <FormField
        rotulo="Valor do veículo"
        erro={errors.auto?.valorVeiculo?.message}
        obrigatorio
        ajuda={`Entre ${formatarMoeda(FAIXAS.valorVeiculo.min)} e ${formatarMoeda(FAIXAS.valorVeiculo.max)}; é o LMI do Casco.`}
      >
        {(campo) => (
          <Controller
            control={control}
            name="auto.valorVeiculo"
            render={({ field }) => (
              <InputMoeda
                {...campo}
                prefixo="R$"
                valor={field.value}
                aoMudar={field.onChange}
                onBlur={field.onBlur}
              />
            )}
          />
        )}
      </FormField>
      <FormField
        rotulo="Ano de fabricação"
        erro={errors.auto?.anoFabricacao?.message}
        obrigatorio
        ajuda={`De ${String(ano - FAIXAS.anosRetroativos)} a ${String(ano + FAIXAS.anosFuturos)}; mais de 10 anos agrava ×1,10.`}
      >
        {(campo) => (
          <Input
            {...campo}
            {...register("auto.anoFabricacao")}
            inputMode="numeric"
            maxLength={4}
            placeholder={String(ano)}
          />
        )}
      </FormField>
      <CampoCep
        nome="auto.cepPernoite"
        rotulo="CEP de pernoite"
        ajuda="O 1º dígito define a região (ex.: 4 = BA/SE, ×1,10)."
      />
      <FormField
        rotulo="Idade do condutor principal"
        erro={errors.auto?.idadeCondutor?.message}
        obrigatorio
        ajuda="De 18 a 90 anos; até 25 anos agrava ×1,40."
      >
        {(campo) => (
          <Input {...campo} {...register("auto.idadeCondutor")} inputMode="numeric" maxLength={2} />
        )}
      </FormField>
      <FormField rotulo="Uso do veículo" ajuda="Uso comercial agrava ×1,35.">
        {(campo) => (
          <Controller
            control={control}
            name="auto.usoComercial"
            render={({ field }) => (
              <SegmentedControl
                aria-labelledby={campo["aria-labelledby"]}
                {...(campo["aria-describedby"]
                  ? { "aria-describedby": campo["aria-describedby"] }
                  : {})}
                opcoes={[
                  { valor: "particular", rotulo: "Particular" },
                  { valor: "comercial", rotulo: "Comercial" },
                ]}
                valor={field.value ? "comercial" : "particular"}
                aoMudar={(v) => field.onChange(v === "comercial")}
                larguraTotal
                tamanho="lg"
              />
            )}
          />
        )}
      </FormField>
      <FormField
        rotulo="Classe de bônus"
        erro={errors.auto?.classeBonus?.message}
        ajuda="Cada classe desconta 3% nos itens com taxa."
      >
        {(campo) => (
          <Controller
            control={control}
            name="auto.classeBonus"
            render={({ field }) => (
              <Select
                {...campo}
                ref={field.ref}
                opcoes={CLASSES_BONUS}
                valor={field.value}
                aoMudar={field.onChange}
              />
            )}
          />
        )}
      </FormField>
    </div>
  );
}

function RiscoResidencial() {
  const {
    control,
    formState: { errors },
  } = useFormContext<FormularioCotacao>();
  return (
    <div className="grid gap-5 md:grid-cols-2">
      <FormField
        rotulo="Valor do imóvel"
        erro={errors.resid?.valorImovel?.message}
        obrigatorio
        ajuda={`Entre ${formatarMoeda(FAIXAS.valorImovel.min)} e ${formatarMoeda(FAIXAS.valorImovel.max)}; é o teto do LMI de Incêndio.`}
      >
        {(campo) => (
          <Controller
            control={control}
            name="resid.valorImovel"
            render={({ field }) => (
              <InputMoeda
                {...campo}
                prefixo="R$"
                valor={field.value}
                aoMudar={field.onChange}
                onBlur={field.onBlur}
              />
            )}
          />
        )}
      </FormField>
      <CampoCep nome="resid.cep" rotulo="CEP do imóvel" ajuda="Somente números." />
      <fieldset className="flex flex-col gap-1.5 md:col-span-2">
        <legend className="mb-1.5 type-label text-ink">Tipo de imóvel</legend>
        <Controller
          control={control}
          name="resid.tipoImovel"
          render={({ field }) => (
            <GrupoCartoesEscolha
              rotulo="Tipo de imóvel"
              colunas={2}
              valor={field.value}
              aoMudar={field.onChange}
              opcoes={[
                {
                  valor: "Apartamento",
                  titulo: "Apartamento",
                  descricao: "Desconto em todos os itens",
                  fator: "×0,80",
                },
                {
                  valor: "Casa",
                  titulo: "Casa",
                  descricao: "Agrava só o Roubo",
                  fator: "×1,30 no roubo",
                },
              ]}
            />
          )}
        />
      </fieldset>
      <FormField rotulo="Ocupação" ajuda="Imóvel de veraneio (não habitual) agrava ×1,30.">
        {(campo) => (
          <Controller
            control={control}
            name="resid.habitual"
            render={({ field }) => (
              <SegmentedControl
                aria-labelledby={campo["aria-labelledby"]}
                {...(campo["aria-describedby"]
                  ? { "aria-describedby": campo["aria-describedby"] }
                  : {})}
                opcoes={[
                  { valor: "habitual", rotulo: "Habitual" },
                  { valor: "veraneio", rotulo: "Veraneio" },
                ]}
                valor={field.value ? "habitual" : "veraneio"}
                aoMudar={(v) => field.onChange(v === "habitual")}
                larguraTotal
                tamanho="lg"
              />
            )}
          />
        )}
      </FormField>
    </div>
  );
}

function RiscoVida() {
  const { control } = useFormContext<FormularioCotacao>();
  const nascimento = useWatch({ control, name: "segurado.dataNascimento" });
  const idade = /^\d{4}-\d{2}-\d{2}$/.test(nascimento)
    ? idadeEm(nascimento, hojeEmBrasilia())
    : null;
  return (
    <div className="grid gap-5 md:grid-cols-2">
      <FormField rotulo="Hábito de fumar" ajuda="Fumante agrava ×1,50 a cobertura de Morte.">
        {(campo) => (
          <Controller
            control={control}
            name="vida.fumante"
            render={({ field }) => (
              <SegmentedControl
                aria-labelledby={campo["aria-labelledby"]}
                {...(campo["aria-describedby"]
                  ? { "aria-describedby": campo["aria-describedby"] }
                  : {})}
                opcoes={[
                  { valor: "nao", rotulo: "Não fumante" },
                  { valor: "sim", rotulo: "Fumante" },
                ]}
                valor={field.value ? "sim" : "nao"}
                aoMudar={(v) => field.onChange(v === "sim")}
                larguraTotal
                tamanho="lg"
              />
            )}
          />
        )}
      </FormField>
      <div className="rounded-lg border border-line bg-surface-2 px-4 py-3 text-sm text-ink-2">
        <p className="font-semibold text-ink">
          Idade do segurado{idade !== null && `: ${String(idade)} anos`}
        </p>
        <p className="mt-1 type-meta text-ink-3">
          Vem da data de nascimento e define a faixa etária da Morte (de ×1,00 a ×12,50).
        </p>
      </div>
    </div>
  );
}

/** Etapa 3: dados de risco do produto (DadosRisco polimórfico do SPEC §4.2). */
export function EtapaRisco() {
  const produto = useWatch<FormularioCotacao, "produto">({ name: "produto" });
  if (produto === "AUTO") return <RiscoAuto />;
  if (produto === "RESID") return <RiscoResidencial />;
  if (produto === "VIDA") return <RiscoVida />;
  return null;
}

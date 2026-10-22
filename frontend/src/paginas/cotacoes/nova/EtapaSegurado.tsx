import { Controller, useFormContext, useWatch } from "react-hook-form";

import { FormField } from "@/ui/componentes/FormField";
import { Input } from "@/ui/componentes/Input";
import { hojeEmBrasilia } from "@/ui/utils/datas";
import { mascararCpfDigitado } from "@/ui/utils/documentos";

import type { FormularioCotacao } from "./modelo";

/** Etapa 2: segurado (SPEC §4.1 — nome 3–120, CPF com dígito verificador, e-mail, nascimento). */
export function EtapaSegurado() {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<FormularioCotacao>();
  const produto = useWatch({ control, name: "produto" });
  return (
    <div className="grid gap-5 md:grid-cols-2">
      <FormField
        rotulo="Nome completo"
        erro={errors.segurado?.nome?.message}
        obrigatorio
        className="md:col-span-2"
      >
        {(campo) => (
          <Input {...campo} {...register("segurado.nome")} autoComplete="name" maxLength={120} />
        )}
      </FormField>
      <FormField
        rotulo="CPF"
        erro={errors.segurado?.cpf?.message}
        obrigatorio
        ajuda="Somente números; a máscara entra sozinha."
      >
        {(campo) => (
          <Controller
            control={control}
            name="segurado.cpf"
            render={({ field }) => (
              <Input
                {...campo}
                ref={field.ref}
                name={field.name}
                value={field.value}
                onBlur={field.onBlur}
                onChange={(e) => field.onChange(mascararCpfDigitado(e.target.value))}
                inputMode="numeric"
                autoComplete="off"
                placeholder="000.000.000-00"
              />
            )}
          />
        )}
      </FormField>
      <FormField
        rotulo="Data de nascimento"
        erro={errors.segurado?.dataNascimento?.message}
        obrigatorio
        ajuda={
          produto === "VIDA"
            ? "No seguro de vida, a idade define a faixa de preço (18 a 70 anos)."
            : undefined
        }
      >
        {(campo) => (
          <Input
            {...campo}
            {...register("segurado.dataNascimento")}
            type="date"
            max={hojeEmBrasilia()}
          />
        )}
      </FormField>
      <FormField
        rotulo="E-mail"
        erro={errors.segurado?.email?.message}
        obrigatorio
        ajuda="Recebe as boas-vindas e os avisos da apólice."
        className="md:col-span-2"
      >
        {(campo) => (
          <Input
            {...campo}
            {...register("segurado.email")}
            type="email"
            autoComplete="email"
            placeholder="nome@exemplo.com.br"
          />
        )}
      </FormField>
    </div>
  );
}

import { zodResolver } from "@hookform/resolvers/zod";
import { LifeBuoy } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate } from "react-router";
import { z } from "zod";

import { useAbrirSinistro } from "@/api/consultas/sinistros";
import type { ApoliceDto } from "@/api/contratos";
import { Button } from "@/ui/componentes/Button";
import { Dialog } from "@/ui/componentes/Dialog";
import { FormField } from "@/ui/componentes/FormField";
import { Input } from "@/ui/componentes/Input";
import { InputMoeda } from "@/ui/componentes/InputMoeda";
import { notificar } from "@/ui/componentes/notificar";
import { Select } from "@/ui/componentes/Select";
import { ContadorCaracteres, Textarea } from "@/ui/componentes/Textarea";
import { hojeEmBrasilia, somarDias } from "@/ui/utils/datas";
import { formatarData, formatarMoeda } from "@/ui/utils/formatar";

import { tratarErroNoFormulario } from "../comum/erros-formulario";
import { RodapeDoDialogo } from "../comum/RodapeDoDialogo";

const DESCRICAO_MINIMA = 20;
const DESCRICAO_MAXIMA = 2000;

/** Última data em que a ocorrência é aceita: hoje, o fim da vigência ou a véspera do cancelamento. */
function ultimaDataPermitida(apolice: ApoliceDto): string {
  const hoje = hojeEmBrasilia();
  const limites = [hoje, apolice.vigenciaFim];
  if (apolice.canceladaEm)
    limites.push(somarDias(hojeEmBrasilia(new Date(apolice.canceladaEm)), -1));
  return limites.sort()[0] ?? hoje;
}

function criarEsquema(apolice: ApoliceDto) {
  const minimo = apolice.vigenciaInicio;
  const maximo = ultimaDataPermitida(apolice);
  return z.object({
    coberturaCodigo: z.string().min(1, "Escolha a cobertura afetada."),
    dataOcorrencia: z
      .string()
      .regex(/^\d{4}-\d{2}-\d{2}$/, "Informe a data da ocorrência.")
      .refine(
        (d) => d >= minimo && d <= maximo,
        `A ocorrência precisa estar entre ${formatarData(minimo)} e ${formatarData(maximo)}.`,
      ),
    descricao: z
      .string()
      .trim()
      .min(
        DESCRICAO_MINIMA,
        `Descreva o ocorrido com pelo menos ${String(DESCRICAO_MINIMA)} caracteres.`,
      )
      .max(DESCRICAO_MAXIMA, `Use no máximo ${String(DESCRICAO_MAXIMA)} caracteres.`),
    valorReclamado: z
      .number({ error: "Informe o valor reclamado." })
      .refine((v) => !Number.isNaN(v) && v > 0, "Informe um valor maior que zero."),
  });
}

type Abertura = z.infer<ReturnType<typeof criarEsquema>>;

function Formulario({ apolice, aoFechar }: { apolice: ApoliceDto; aoFechar: () => void }) {
  const navegar = useNavigate();
  const abrir = useAbrirSinistro();
  const [chaveIdempotencia] = useState(() => crypto.randomUUID());
  const coberturas = apolice.coberturas.filter((c) => c.permiteSinistro);
  const {
    control,
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Abertura>({
    resolver: zodResolver(criarEsquema(apolice)),
    defaultValues: { coberturaCodigo: "", dataOcorrencia: "", descricao: "" },
    mode: "onTouched",
  });
  const codigo = useWatch({ control, name: "coberturaCodigo" });
  const descricao = useWatch({ control, name: "descricao" });
  const lmi = coberturas.find((c) => c.codigo === codigo)?.lmi;

  async function enviar(dados: Abertura) {
    try {
      const sinistro = await abrir.mutateAsync({
        pedido: { apoliceId: apolice.id, ...dados, descricao: dados.descricao.trim() },
        chaveIdempotencia,
      });
      notificar.sucesso({
        titulo: `Sinistro ${sinistro.numero} aberto`,
        descricao: "A triagem automática chega em instantes.",
      });
      void navegar(`/sinistros/${sinistro.id}`);
    } catch (erro) {
      tratarErroNoFormulario(erro, setError, {
        coberturaCodigo: "coberturaCodigo",
        dataOcorrencia: "dataOcorrencia",
        descricao: "descricao",
        valorReclamado: "valorReclamado",
      });
    }
  }

  return (
    <form noValidate onSubmit={(e) => void handleSubmit(enviar)(e)}>
      <div className="grid gap-5 md:grid-cols-2">
        <FormField
          rotulo="Cobertura afetada"
          obrigatorio
          erro={errors.coberturaCodigo?.message}
          ajuda={
            lmi === undefined
              ? "Só aparecem as coberturas que permitem sinistro."
              : `LMI da cobertura: ${formatarMoeda(lmi)}`
          }
        >
          {(campo) => (
            <Controller
              control={control}
              name="coberturaCodigo"
              render={({ field }) => (
                <Select
                  {...campo}
                  ref={field.ref}
                  placeholder="Escolha a cobertura"
                  opcoes={coberturas.map((c) => ({ valor: c.codigo, rotulo: c.nome }))}
                  valor={field.value || undefined}
                  aoMudar={field.onChange}
                />
              )}
            />
          )}
        </FormField>
        <FormField
          rotulo="Data da ocorrência"
          obrigatorio
          erro={errors.dataOcorrencia?.message}
          ajuda="Dentro da vigência e até hoje."
        >
          {(campo) => (
            <Input
              {...campo}
              {...register("dataOcorrencia")}
              type="date"
              min={apolice.vigenciaInicio}
              max={ultimaDataPermitida(apolice)}
            />
          )}
        </FormField>
        <FormField
          rotulo="Descrição do ocorrido"
          obrigatorio
          erro={errors.descricao?.message}
          ajuda="O que aconteceu, onde e quais bens foram atingidos."
          contador={<ContadorCaracteres atual={descricao.length} maximo={DESCRICAO_MAXIMA} />}
          className="md:col-span-2"
        >
          {(campo) => (
            <Textarea {...campo} {...register("descricao")} maxLength={DESCRICAO_MAXIMA} rows={4} />
          )}
        </FormField>
        <FormField
          rotulo="Valor reclamado"
          obrigatorio
          erro={errors.valorReclamado?.message}
          ajuda="A indenização fica limitada ao LMI da cobertura."
        >
          {(campo) => (
            <Controller
              control={control}
              name="valorReclamado"
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
      </div>
      <RodapeDoDialogo>
        <Button variante="secundario" onClick={aoFechar} desabilitado={abrir.isPending}>
          Voltar
        </Button>
        <Button
          type="submit"
          icone={LifeBuoy}
          carregando={abrir.isPending}
          rotuloCarregando="Abrindo…"
        >
          Abrir sinistro
        </Button>
      </RodapeDoDialogo>
    </form>
  );
}

interface DialogoAbrirSinistroProps {
  apolice: ApoliceDto;
  aberto: boolean;
  aoMudar: (aberto: boolean) => void;
}

/** Aviso de sinistro: cobertura que permite sinistro, data na vigência, descrição 20–2000 e valor. */
export function DialogoAbrirSinistro({ apolice, aberto, aoMudar }: DialogoAbrirSinistroProps) {
  return (
    <Dialog
      aberto={aberto}
      aoMudar={aoMudar}
      titulo={`Abrir sinistro na apólice ${apolice.numero}`}
      descricao={`Segurado ${apolice.segurado.nome}. O sinistro entra na fila de regulação dos analistas.`}
      largura="formulario"
    >
      <Formulario apolice={apolice} aoFechar={() => aoMudar(false)} />
    </Dialog>
  );
}

import { zodResolver } from "@hookform/resolvers/zod";
import { ShieldCheck } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate } from "react-router";
import { z } from "zod";

import { useEmitirApolice } from "@/api/consultas/apolices";
import type { CotacaoDto } from "@/api/contratos";
import { Button } from "@/ui/componentes/Button";
import { Dialog } from "@/ui/componentes/Dialog";
import { FormField } from "@/ui/componentes/FormField";
import { Input } from "@/ui/componentes/Input";
import { notificar } from "@/ui/componentes/notificar";
import { Select } from "@/ui/componentes/Select";
import { hojeEmBrasilia, somarDias } from "@/ui/utils/datas";
import { formatarData, formatarMoeda } from "@/ui/utils/formatar";
import { calcularParcelas } from "@/ui/utils/parcelas";

import { tratarErroNoFormulario } from "../comum/erros-formulario";
import { RodapeDoDialogo } from "../comum/RodapeDoDialogo";

const JANELA_INICIO_DIAS = 30;

function criarEsquema(hoje: string, maxParcelas: number) {
  const limite = somarDias(hoje, JANELA_INICIO_DIAS);
  return z.object({
    inicioVigencia: z
      .string()
      .regex(/^\d{4}-\d{2}-\d{2}$/, "Informe o início da vigência.")
      .refine(
        (d) => d >= hoje && d <= limite,
        `Escolha uma data entre ${formatarData(hoje)} e ${formatarData(limite)}.`,
      ),
    parcelas: z.string().refine(
      (p) => {
        const n = Number(p);
        return Number.isInteger(n) && n >= 1 && n <= maxParcelas;
      },
      `Escolha de 1 a ${String(maxParcelas)} parcelas.`,
    ),
  });
}

type Emissao = z.infer<ReturnType<typeof criarEsquema>>;

function fimDaVigencia(inicio: string): string {
  const [ano, mes, dia] = inicio.split("-");
  return `${String(Number(ano) + 1)}-${mes ?? "01"}-${dia ?? "01"}`;
}

/** Conteúdo montado só com o diálogo aberto: uma Idempotency-Key nova por submissão (abertura). */
function FormularioEmissao({
  cotacao,
  aoCancelar,
}: {
  cotacao: CotacaoDto;
  aoCancelar: () => void;
}) {
  const navegar = useNavigate();
  const emitir = useEmitirApolice();
  const [chaveIdempotencia] = useState(() => crypto.randomUUID());
  const hoje = hojeEmBrasilia();
  const {
    control,
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Emissao>({
    resolver: zodResolver(criarEsquema(hoje, cotacao.maxParcelas)),
    defaultValues: { inicioVigencia: hoje, parcelas: String(cotacao.maxParcelas) },
  });
  const inicio = useWatch({ control, name: "inicioVigencia" });
  const parcelas = Number(useWatch({ control, name: "parcelas" }));
  const plano = parcelas >= 1 ? calcularParcelas(cotacao.premioTotal, parcelas) : null;
  const opcoes = Array.from({ length: cotacao.maxParcelas }, (_, i) => {
    const n = i + 1;
    const p = calcularParcelas(cotacao.premioTotal, n);
    return {
      valor: String(n),
      rotulo:
        n === 1
          ? `À vista · ${formatarMoeda(p.primeira)}`
          : `${String(n)}× · 1ª de ${formatarMoeda(p.primeira)}`,
    };
  });

  async function enviar(dados: Emissao) {
    try {
      const apolice = await emitir.mutateAsync({
        pedido: {
          cotacaoId: cotacao.id,
          inicioVigencia: dados.inicioVigencia,
          parcelas: Number(dados.parcelas),
        },
        chaveIdempotencia,
      });
      notificar.sucesso({
        titulo: `Apólice ${apolice.numero} emitida`,
        descricao: "As boas-vindas ao segurado seguem pela fila de mensagens.",
      });
      void navegar(`/apolices/${apolice.id}`);
    } catch (erro) {
      tratarErroNoFormulario(erro, setError, {
        inicioVigencia: "inicioVigencia",
        parcelas: "parcelas",
      });
    }
  }

  return (
    <form noValidate onSubmit={(e) => void handleSubmit(enviar)(e)} className="flex flex-col gap-5">
      <FormField
        rotulo="Início da vigência"
        obrigatorio
        erro={errors.inicioVigencia?.message}
        ajuda={
          /^\d{4}-\d{2}-\d{2}$/.test(inicio)
            ? `Vigência de ${formatarData(inicio)} a ${formatarData(fimDaVigencia(inicio))}.`
            : "Até 30 dias a partir de hoje."
        }
      >
        {(campo) => (
          <Input
            {...campo}
            {...register("inicioVigencia")}
            type="date"
            min={hoje}
            max={somarDias(hoje, JANELA_INICIO_DIAS)}
          />
        )}
      </FormField>
      <FormField
        rotulo="Parcelas"
        obrigatorio
        erro={errors.parcelas?.message}
        ajuda={
          plano && parcelas > 1
            ? `1ª de ${formatarMoeda(plano.primeira)} + ${String(parcelas - 1)}× de ${formatarMoeda(plano.demais)}. A 1ª parcela absorve o ajuste de centavos.`
            : `Até ${String(cotacao.maxParcelas)}× (parcela mínima de R$ 60,00).`
        }
      >
        {(campo) => (
          <Controller
            control={control}
            name="parcelas"
            render={({ field }) => (
              <Select
                {...campo}
                ref={field.ref}
                opcoes={opcoes}
                valor={field.value}
                aoMudar={field.onChange}
              />
            )}
          />
        )}
      </FormField>
      <RodapeDoDialogo>
        <Button variante="secundario" onClick={aoCancelar} desabilitado={emitir.isPending}>
          Voltar
        </Button>
        <Button
          type="submit"
          icone={ShieldCheck}
          carregando={emitir.isPending}
          rotuloCarregando="Emitindo…"
        >
          Emitir apólice
        </Button>
      </RodapeDoDialogo>
    </form>
  );
}

export function DialogoEmitir({ cotacao }: { cotacao: CotacaoDto }) {
  const [aberto, setAberto] = useState(false);
  return (
    <Dialog
      aberto={aberto}
      aoMudar={setAberto}
      gatilho={
        <Button icone={ShieldCheck} className="max-md:w-full">
          Emitir apólice
        </Button>
      }
      titulo="Emitir apólice"
      descricao={`Prêmio total de ${formatarMoeda(cotacao.premioTotal)}. A vigência é de um ano a partir do início escolhido.`}
    >
      <FormularioEmissao cotacao={cotacao} aoCancelar={() => setAberto(false)} />
    </Dialog>
  );
}

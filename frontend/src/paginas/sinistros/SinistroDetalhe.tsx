import { zodResolver } from "@hookform/resolvers/zod";
import { Ban, Banknote, Check, LoaderCircle, Play } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { Link, useParams } from "react-router";
import { z } from "zod";

import {
  useAprovarSinistro,
  useIniciarAnalise,
  useNegarSinistro,
  useRegistrarPagamento,
  useSinistro,
} from "@/api/consultas/sinistros";
import { useFeatures } from "@/api/consultas/sistema";
import type { SinistroDto, StatusSinistro } from "@/api/contratos";
import { pode } from "@/app/sessao/permissoes";
import { useUsuario } from "@/app/sessao/sessao-contexto";
import { CarregandoPagina } from "@/app/shell/CarregandoPagina";
import { Pagina } from "@/app/shell/Pagina";
import { StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { ConfirmDialog, Dialog } from "@/ui/componentes/Dialog";
import { FormField } from "@/ui/componentes/FormField";
import { InputMoeda } from "@/ui/componentes/InputMoeda";
import { notificar } from "@/ui/componentes/notificar";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { ContadorCaracteres, Textarea } from "@/ui/componentes/Textarea";
import { Timeline, type EventoTimeline, type TomEvento } from "@/ui/componentes/Timeline";
import { DateText, Money } from "@/ui/componentes/Valores";
import { cn } from "@/ui/utils/cn";
import { formatarDataHora, formatarMoeda } from "@/ui/utils/formatar";

import { EstadoDaConsulta } from "../comum/EstadoDaConsulta";
import { tratarErroNoFormulario } from "../comum/erros-formulario";
import { RodapeDoDialogo } from "../comum/RodapeDoDialogo";
import { rotuloAlerta } from "../comum/rotulos";
import { ScoreTriagem } from "./ScoreTriagem";

const TEXTO_MAXIMO = 2000;
const MOTIVO_MINIMO = 10;

const TOM_DO_STATUS: Record<StatusSinistro, TomEvento> = {
  Aberto: "warn",
  EmAnalise: "steel",
  Aprovado: "ok",
  Negado: "bad",
  Pago: "ok",
};

const TITULO_DO_STATUS: Record<StatusSinistro, string> = {
  Aberto: "Sinistro aberto",
  EmAnalise: "Análise iniciada",
  Aprovado: "Indenização aprovada",
  Negado: "Sinistro negado",
  Pago: "Pagamento registrado",
};

function tetoDaIndenizacao(sinistro: SinistroDto): number {
  return Math.min(sinistro.valorReclamado, sinistro.lmiCobertura);
}

/* ---------------------------------------------------------------- diálogos do analista */

function FormularioAprovacao({
  sinistro,
  aoFechar,
}: {
  sinistro: SinistroDto;
  aoFechar: () => void;
}) {
  const aprovar = useAprovarSinistro();
  const teto = tetoDaIndenizacao(sinistro);
  const esquema = z.object({
    valorIndenizacao: z
      .number({ error: "Informe o valor da indenização." })
      .refine(
        (v) => !Number.isNaN(v) && v > 0 && v <= teto,
        `Informe um valor maior que zero e de até ${formatarMoeda(teto)}.`,
      ),
    observacao: z.string().max(TEXTO_MAXIMO, `Use no máximo ${String(TEXTO_MAXIMO)} caracteres.`),
  });
  type Aprovacao = z.infer<typeof esquema>;
  const {
    control,
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Aprovacao>({
    resolver: zodResolver(esquema),
    defaultValues: { valorIndenizacao: teto, observacao: "" },
  });
  const observacao = useWatch({ control, name: "observacao" });

  async function enviar(dados: Aprovacao) {
    try {
      await aprovar.mutateAsync({
        id: sinistro.id,
        pedido: {
          valorIndenizacao: dados.valorIndenizacao,
          observacao: dados.observacao.trim() || null,
        },
      });
      notificar.sucesso({
        titulo: `Sinistro ${sinistro.numero} aprovado`,
        descricao: `Indenização de ${formatarMoeda(dados.valorIndenizacao)}.`,
      });
      aoFechar();
    } catch (erro) {
      tratarErroNoFormulario(erro, setError, {
        valorIndenizacao: "valorIndenizacao",
        observacao: "observacao",
      });
    }
  }

  return (
    <form noValidate onSubmit={(e) => void handleSubmit(enviar)(e)} className="flex flex-col gap-5">
      <FormField
        rotulo="Valor da indenização"
        obrigatorio
        erro={errors.valorIndenizacao?.message}
        ajuda={`Até ${formatarMoeda(teto)}: o menor entre o reclamado (${formatarMoeda(sinistro.valorReclamado)}) e o LMI (${formatarMoeda(sinistro.lmiCobertura)}).`}
      >
        {(campo) => (
          <Controller
            control={control}
            name="valorIndenizacao"
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
        rotulo="Observação"
        erro={errors.observacao?.message}
        ajuda="Opcional. Fica no histórico do sinistro."
        contador={<ContadorCaracteres atual={observacao.length} maximo={TEXTO_MAXIMO} />}
      >
        {(campo) => (
          <Textarea {...campo} {...register("observacao")} maxLength={TEXTO_MAXIMO} rows={3} />
        )}
      </FormField>
      <RodapeDoDialogo>
        <Button variante="secundario" onClick={aoFechar} desabilitado={aprovar.isPending}>
          Voltar
        </Button>
        <Button
          type="submit"
          icone={Check}
          carregando={aprovar.isPending}
          rotuloCarregando="Aprovando…"
        >
          Aprovar indenização
        </Button>
      </RodapeDoDialogo>
    </form>
  );
}

function FormularioNegativa({
  sinistro,
  aoFechar,
}: {
  sinistro: SinistroDto;
  aoFechar: () => void;
}) {
  const negar = useNegarSinistro();
  const esquema = z.object({
    motivo: z
      .string()
      .trim()
      .min(MOTIVO_MINIMO, `Explique o motivo com pelo menos ${String(MOTIVO_MINIMO)} caracteres.`)
      .max(TEXTO_MAXIMO, `Use no máximo ${String(TEXTO_MAXIMO)} caracteres.`),
  });
  type Negativa = z.infer<typeof esquema>;
  const {
    control,
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Negativa>({
    resolver: zodResolver(esquema),
    defaultValues: { motivo: "" },
  });
  const motivo = useWatch({ control, name: "motivo" });

  async function enviar(dados: Negativa) {
    try {
      await negar.mutateAsync({ id: sinistro.id, motivo: dados.motivo.trim() });
      notificar.info({
        titulo: `Sinistro ${sinistro.numero} negado`,
        descricao: "O segurado recebe o motivo por e-mail.",
      });
      aoFechar();
    } catch (erro) {
      tratarErroNoFormulario(erro, setError, { motivo: "motivo" });
    }
  }

  return (
    <form noValidate onSubmit={(e) => void handleSubmit(enviar)(e)} className="flex flex-col gap-5">
      <FormField
        rotulo="Motivo da negativa"
        obrigatorio
        erro={errors.motivo?.message}
        ajuda="Vai para o histórico e para o e-mail ao segurado."
        contador={<ContadorCaracteres atual={motivo.length} maximo={TEXTO_MAXIMO} />}
      >
        {(campo) => (
          <Textarea {...campo} {...register("motivo")} maxLength={TEXTO_MAXIMO} rows={4} />
        )}
      </FormField>
      <RodapeDoDialogo>
        <Button variante="secundario" onClick={aoFechar} desabilitado={negar.isPending}>
          Voltar
        </Button>
        <Button
          type="submit"
          variante="perigo"
          icone={Ban}
          carregando={negar.isPending}
          rotuloCarregando="Negando…"
        >
          Negar sinistro
        </Button>
      </RodapeDoDialogo>
    </form>
  );
}

function AcoesDoAnalista({ sinistro }: { sinistro: SinistroDto }) {
  const usuario = useUsuario();
  const iniciar = useIniciarAnalise();
  const pagar = useRegistrarPagamento();
  const [dialogo, setDialogo] = useState<"aprovar" | "negar" | null>(null);
  if (!pode(usuario.papel, "analisarSinistros")) return null;
  const fechar = () => setDialogo(null);

  if (sinistro.status === "Aberto") {
    return (
      <ConfirmDialog
        gatilho={<Button icone={Play}>Iniciar análise</Button>}
        titulo="Iniciar a análise?"
        descricao={`O sinistro ${sinistro.numero} passa para "Em análise" sob a sua responsabilidade.`}
        rotuloConfirmar="Iniciar análise"
        rotuloConfirmando="Iniciando…"
        tom="primario"
        aoConfirmar={async () => {
          await iniciar.mutateAsync(sinistro.id);
          notificar.sucesso({ titulo: "Análise iniciada" });
        }}
      />
    );
  }
  if (sinistro.status === "EmAnalise") {
    return (
      <>
        <Dialog
          aberto={dialogo === "negar"}
          aoMudar={(aberto) => setDialogo(aberto ? "negar" : null)}
          gatilho={
            <Button variante="perigo-contorno" icone={Ban}>
              Negar
            </Button>
          }
          titulo={`Negar o sinistro ${sinistro.numero}`}
          descricao="A negativa encerra o sinistro. Explique o motivo na voz do negócio."
        >
          <FormularioNegativa sinistro={sinistro} aoFechar={fechar} />
        </Dialog>
        <Dialog
          aberto={dialogo === "aprovar"}
          aoMudar={(aberto) => setDialogo(aberto ? "aprovar" : null)}
          gatilho={<Button icone={Check}>Aprovar</Button>}
          titulo={`Aprovar o sinistro ${sinistro.numero}`}
          descricao={`${sinistro.coberturaNome} · reclamado ${formatarMoeda(sinistro.valorReclamado)}.`}
        >
          <FormularioAprovacao sinistro={sinistro} aoFechar={fechar} />
        </Dialog>
      </>
    );
  }
  if (sinistro.status === "Aprovado") {
    return (
      <ConfirmDialog
        gatilho={<Button icone={Banknote}>Registrar pagamento</Button>}
        titulo="Registrar o pagamento?"
        descricao={`Confirma o pagamento de ${formatarMoeda(sinistro.valorIndenizacao ?? 0)} ao segurado. O sinistro passa para "Pago".`}
        rotuloConfirmar="Registrar pagamento"
        rotuloConfirmando="Registrando…"
        tom="primario"
        aoConfirmar={async () => {
          await pagar.mutateAsync(sinistro.id);
          notificar.sucesso({ titulo: "Pagamento registrado" });
        }}
      />
    );
  }
  return null;
}

/* ---------------------------------------------------------------- conteúdo */

function Dado({
  rotulo,
  children,
  className,
}: {
  rotulo: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={className}>
      <dt className="type-meta text-ink-3">{rotulo}</dt>
      <dd className="mt-0.5 text-sm text-ink tnum">{children}</dd>
    </div>
  );
}

function CartaoTriagem({ sinistro }: { sinistro: SinistroDto }) {
  const { data: features } = useFeatures();
  const triagem = sinistro.triagem;
  return (
    <Card>
      <CardHeader titulo="Triagem automática" subtitulo="Score de 0 a 100 calculado pelo worker" />
      <CardBody className="pt-3">
        {triagem ? (
          <>
            <div className="flex items-end justify-between gap-3">
              <p className="font-serif text-[2.25rem] leading-none font-medium text-ink tnum">
                {triagem.score}
                <span className="ml-1 font-sans text-sm font-medium text-ink-3">/100</span>
              </p>
              <ScoreTriagem score={triagem.score} />
            </div>
            <div aria-hidden className="mt-3 h-1 rounded-full bg-meter-track">
              <div
                className={cn(
                  "h-full rounded-full",
                  triagem.score >= 60 ? "bg-bad" : triagem.score >= 30 ? "bg-warn" : "bg-ok",
                )}
                style={{ width: `${String(triagem.score)}%` }}
              />
            </div>
            {triagem.alertas.length === 0 ? (
              <p className="mt-4 text-sm text-ink-2">
                Nenhum alerta: sem sinal de risco nas regras de triagem.
              </p>
            ) : (
              <ul className="mt-4 flex flex-col gap-2" aria-label="Alertas da triagem">
                {triagem.alertas.map((a) => (
                  <li key={a} className="flex items-start gap-2 text-sm text-ink-2">
                    <span aria-hidden className="mt-1.5 size-1.5 shrink-0 rounded-full bg-warn" />
                    {rotuloAlerta(a)}
                  </li>
                ))}
              </ul>
            )}
            <p className="mt-4 type-meta text-ink-3">
              Triado em <DateText valor={triagem.triadoEm} formato="data-hora" />
            </p>
          </>
        ) : features?.triagemAutomaticaSinistros === false ? (
          <p className="text-sm text-ink-3">A triagem automática está desligada neste ambiente.</p>
        ) : (
          <p role="status" className="flex items-center gap-2 text-sm text-ink-2">
            <LoaderCircle
              size={15}
              className="motion-safe:animate-[girar_800ms_linear_infinite]"
              aria-hidden
            />
            Triagem em andamento pela fila de mensagens…
          </p>
        )}
      </CardBody>
    </Card>
  );
}

function eventosDoHistorico(sinistro: SinistroDto): EventoTimeline[] {
  return sinistro.historico.map((h, i) => ({
    id: `${String(i)}-${h.status}`,
    quando: h.ocorridoEm,
    tom: TOM_DO_STATUS[h.status],
    titulo: TITULO_DO_STATUS[h.status],
    descricao: (
      <>
        {h.usuarioNome ? `Por ${h.usuarioNome}` : "Ação automática"}
        {h.observacao && <span className="mt-1 block text-ink">“{h.observacao}”</span>}
      </>
    ),
    extra: <StatusBadge entidade="sinistro" status={h.status} />,
  }));
}

function Conteudo({ sinistro }: { sinistro: SinistroDto }) {
  return (
    <Pagina
      titulo={`Sinistro ${sinistro.numero}`}
      subtitulo={
        <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
          <StatusBadge entidade="sinistro" status={sinistro.status} />
          <span>
            Apólice{" "}
            <Link
              to={`/apolices/${sinistro.apoliceId}`}
              className="mono-id text-link hover:underline"
            >
              {sinistro.apoliceNumero}
            </Link>{" "}
            · {sinistro.seguradoNome}
          </span>
        </span>
      }
      migalhas={[
        { rotulo: "Sinistros", href: "/sinistros" },
        { rotulo: sinistro.numero, codigo: true },
      ]}
      acoes={<AcoesDoAnalista sinistro={sinistro} />}
    >
      <div className="grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-[minmax(0,1fr)_380px] lg:items-start">
        <div className="flex flex-col gap-4 md:gap-6">
          <Card>
            <CardHeader
              titulo="Ocorrência"
              subtitulo={`Aviso registrado em ${formatarDataHora(sinistro.abertoEm)}`}
            />
            <CardBody className="pt-4">
              <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-3">
                <Dado rotulo="Data da ocorrência">
                  <DateText valor={sinistro.dataOcorrencia} />
                </Dado>
                <Dado rotulo="Cobertura">
                  {sinistro.coberturaNome}{" "}
                  <span className="mono-id text-ink-3">{sinistro.coberturaCodigo}</span>
                </Dado>
                <Dado rotulo="Produto">
                  <ProdutoTag produto={sinistro.produtoCodigo} />
                </Dado>
                <Dado rotulo="Valor reclamado">
                  <Money valor={sinistro.valorReclamado} className="font-semibold" />
                </Dado>
                <Dado rotulo="LMI da cobertura">
                  <Money valor={sinistro.lmiCobertura} />
                </Dado>
                <Dado rotulo="Indenização">
                  {sinistro.valorIndenizacao === null ? (
                    <span className="text-ink-3">—</span>
                  ) : (
                    <Money valor={sinistro.valorIndenizacao} className="font-semibold" />
                  )}
                </Dado>
                <Dado rotulo="Descrição" className="sm:col-span-3">
                  <span className="whitespace-pre-line text-ink-2">{sinistro.descricao}</span>
                </Dado>
                {sinistro.motivoNegativa && (
                  <Dado rotulo="Motivo da negativa" className="sm:col-span-3">
                    <span className="block rounded-md border border-bad-line bg-bad-bg px-3 py-2 text-bad">
                      {sinistro.motivoNegativa}
                    </span>
                  </Dado>
                )}
              </dl>
            </CardBody>
          </Card>
          <Card>
            <CardHeader
              titulo="Histórico"
              subtitulo="Cada transição de status, com quem fez e quando"
            />
            <CardBody className="pt-4">
              <Timeline eventos={eventosDoHistorico(sinistro)} rotulo="Histórico do sinistro" />
            </CardBody>
          </Card>
        </div>
        <aside aria-label="Triagem e partes do sinistro" className="flex flex-col gap-4 md:gap-6">
          <CartaoTriagem sinistro={sinistro} />
          <Card>
            <CardHeader titulo="Apólice" />
            <CardBody className="pt-3">
              <dl className="flex flex-col gap-3">
                <Dado rotulo="Número">
                  <Link
                    to={`/apolices/${sinistro.apoliceId}`}
                    className="mono-id text-link hover:underline"
                  >
                    {sinistro.apoliceNumero}
                  </Link>
                </Dado>
                <Dado rotulo="Segurado">{sinistro.seguradoNome}</Dado>
                <Dado rotulo="Corretor">{sinistro.corretorNome}</Dado>
              </dl>
            </CardBody>
          </Card>
        </aside>
      </div>
    </Pagina>
  );
}

export default function SinistroDetalhe() {
  const { id = "" } = useParams();
  const { data: features } = useFeatures();
  const consulta = useSinistro(id, features?.triagemAutomaticaSinistros ?? true);
  if (consulta.isPending) return <CarregandoPagina />;
  if (consulta.isError) {
    return (
      <Pagina
        titulo="Sinistro"
        migalhas={[{ rotulo: "Sinistros", href: "/sinistros" }, { rotulo: "Sinistro" }]}
      >
        <EstadoDaConsulta
          erro={consulta.error}
          entidade="o sinistro"
          voltarPara={{ rotulo: "Voltar aos sinistros", href: "/sinistros" }}
          aoTentarDeNovo={() => void consulta.refetch()}
        />
      </Pagina>
    );
  }
  return <Conteudo sinistro={consulta.data} />;
}

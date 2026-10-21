import { zodResolver } from "@hookform/resolvers/zod";
import {
  Ban,
  Bell,
  Clock,
  Download,
  Eye,
  EyeOff,
  FilePen,
  Mail,
  MoreHorizontal,
  Plus,
  Search,
  Send,
  Trash2,
  TriangleAlert,
} from "lucide-react";
import { useId, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";

import { Avatar } from "@/ui/componentes/Avatar";
import {
  Badge,
  StatusBadge,
  type StatusApolice,
  type StatusCotacao,
  type StatusSinistro,
} from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Checkbox } from "@/ui/componentes/Checkbox";
import { FormField } from "@/ui/componentes/FormField";
import { IconButton } from "@/ui/componentes/IconButton";
import { Input } from "@/ui/componentes/Input";
import { InputMoeda } from "@/ui/componentes/InputMoeda";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { SegmentedControl } from "@/ui/componentes/SegmentedControl";
import { Select } from "@/ui/componentes/Select";
import { SeloObrigatoria, Switch } from "@/ui/componentes/Switch";
import { ContadorCaracteres, Textarea } from "@/ui/componentes/Textarea";
import { DateText, Money, Percent } from "@/ui/componentes/Valores";
import { NumeroKpi } from "@/ui/componentes/KpiCard";
import { Gravura, Marca } from "@/ui/marca/Marca";
import { formatarMoeda } from "@/ui/utils/formatar";

import { HOJE } from "./dados";

const Rotulo = ({ children }: { children: string }) => (
  <p className="mb-2 type-caption text-ink-3">{children}</p>
);

export function DemoTipografia() {
  return (
    <div className="flex flex-col gap-4">
      <p className="type-display-lg text-ink">AUTO-2026-000042</p>
      <p className="type-title-page text-ink">Visão da carteira</p>
      <p className="type-title-card text-ink">Prêmio emitido</p>
      <p className="max-w-prose text-sm text-ink">
        Texto base de interface em Public Sans 14/21.{" "}
        <span className="text-ink-2">Secundário em --ink-2.</span>{" "}
        <span className="text-ink-3">Metadado em --ink-3.</span>{" "}
        <a href="#tipografia" className="font-semibold text-link hover:underline">
          Link
        </a>
      </p>
      <p className="text-sm text-ink-2">
        Identificador em Geist Mono: <span className="mono-id text-ink">SIN-2026-000097</span> ·
        cobertura <span className="mono-id text-ink">RCF_DM</span>
      </p>
      <div className="flex flex-wrap items-end gap-x-8 gap-y-3">
        <div>
          <Rotulo>Herói (até 2 por tela)</Rotulo>
          <Money valor={4482.93} variante="heroi" className="type-fig-hero text-ink" />
        </div>
        <div>
          <Rotulo>KPI abreviado</Rotulo>
          <NumeroKpi valor={{ tipo: "moeda", valor: 286412.9 }} />
        </div>
        <div>
          <Rotulo>KPI percentual</Rotulo>
          <NumeroKpi valor={{ tipo: "percentual", valor: 0.542 }} />
        </div>
      </div>
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
        <dt className="text-ink-3">Moeda (tabela)</dt>
        <dd>
          <Money valor={4528.2} />
        </dd>
        <dt className="text-ink-3">Data</dt>
        <dd>
          <DateText valor="2026-10-21" />
        </dd>
        <dt className="text-ink-3">Data e hora</dt>
        <dd>
          <DateText valor="2026-10-21T14:32:00-03:00" formato="data-hora" />
        </dd>
        <dt className="text-ink-3">Relativo</dt>
        <dd>
          <DateText valor="2026-10-21T14:24:00-03:00" formato="relativo" agora={HOJE} />
        </dd>
        <dt className="text-ink-3">IOF</dt>
        <dd>
          <Percent valor={0.0738} /> · Vida <Percent valor={0.0038} />
        </dd>
      </dl>
    </div>
  );
}

export function DemoMarca() {
  return (
    // Fio dourado no escuro (--brand-surface-line): marinho sobre a noite precisa de contorno.
    <div className="on-navy relative overflow-hidden rounded-2xl border border-brand-line bg-brand p-6 text-on-navy">
      <Gravura tamanho="lg" className="absolute -top-[250px] -right-[150px] size-[520px]" />
      <Marca className="relative" />
      <p className="relative mt-6 type-display-lg">AUTO-2026-000042</p>
      <div className="relative mt-3 flex flex-wrap items-center gap-2">
        <StatusBadge entidade="apolice" status="Ativa" sobreMarinho />
        <Badge tom="warn" sobreMarinho marcador={TriangleAlert}>
          1 sinistro em análise
        </Badge>
      </div>
      <div className="relative mt-5 flex flex-wrap gap-2">
        <Button variante="marinho-contorno" icone={Download}>
          Baixar PDF
        </Button>
        <Button variante="marinho-claro" icone={FilePen}>
          Endossar
        </Button>
        <Button variante="marinho-perigo" icone={Ban}>
          Cancelar
        </Button>
      </div>
    </div>
  );
}

export function DemoBotoes() {
  const [carregando, setCarregando] = useState(false);
  return (
    <div className="flex flex-col gap-5">
      <div>
        <Rotulo>Variantes</Rotulo>
        <div className="flex flex-wrap items-center gap-2">
          <Button icone={Plus}>Nova cotação</Button>
          <Button variante="secundario" icone={Download}>
            Exportar
          </Button>
          <Button variante="fantasma">Voltar para Risco</Button>
          <Button variante="perigo">Cancelar apólice</Button>
          <Button variante="perigo-contorno" icone={Trash2}>
            Excluir rascunho
          </Button>
        </div>
      </div>
      <div>
        <Rotulo>Tamanhos, carregando e desabilitado</Rotulo>
        <div className="flex flex-wrap items-center gap-2">
          <Button tamanho="sm" variante="secundario">
            Pequeno
          </Button>
          <Button tamanho="lg">Entrar</Button>
          <Button
            icone={Send}
            carregando={carregando}
            rotuloCarregando="Emitindo…"
            onClick={() => {
              setCarregando(true);
              window.setTimeout(() => setCarregando(false), 1800);
            }}
          >
            Emitir apólice
          </Button>
          <Button desabilitado>Revisar cotação</Button>
        </div>
      </div>
      <div>
        <Rotulo>Botões de ícone</Rotulo>
        <div className="flex flex-wrap items-center gap-2">
          <IconButton icone={Search} rotulo="Buscar" comTooltip />
          <IconButton icone={Bell} rotulo="Notificações, 3 não lidas" indicador comTooltip />
          <IconButton icone={MoreHorizontal} rotulo="Mais ações" variante="secundario" />
          <IconButton icone={Mail} rotulo="Reenviar e-mail" tamanho="sm" />
        </div>
      </div>
    </div>
  );
}

const MOTIVOS_IMOVEL = [
  { valor: "casa", rotulo: "Casa" },
  { valor: "apartamento", rotulo: "Apartamento" },
] as const;

const OPCOES_ASSISTENCIA = [
  { valor: "200", rotulo: "Reboque até 200 km" },
  { valor: "400", rotulo: "Reboque até 400 km" },
  { valor: "ilimitado", rotulo: "Reboque ilimitado", desabilitada: true },
];

const LMI_MINIMO = 50_000;
const LMI_MAXIMO = 500_000;
const DESCRICAO_MAXIMA = 2_000;

const esquemaSegurado = z.object({
  nome: z.string().trim().min(3, "Informe o nome completo do segurado."),
  email: z.email("Informe um e-mail válido, como nome@corretora.com.br."),
  lmi: z
    .number({ error: "Informe o LMI em reais." })
    .min(
      LMI_MINIMO,
      `Informe um LMI entre ${formatarMoeda(LMI_MINIMO)} e ${formatarMoeda(LMI_MAXIMO)}.`,
    )
    .max(
      LMI_MAXIMO,
      `Informe um LMI entre ${formatarMoeda(LMI_MINIMO)} e ${formatarMoeda(LMI_MAXIMO)}.`,
    ),
  assistencia: z.string().min(1, "Escolha a assistência."),
});

type DadosSegurado = z.infer<typeof esquemaSegurado>;

/** Formulário real com React Hook Form + Zod: mensagens dizem o que fazer, nunca só "inválido". */
export function DemoFormulario() {
  const id = useId();
  const [senhaVisivel, setSenhaVisivel] = useState(false);
  const [imovel, setImovel] = useState<"casa" | "apartamento">("apartamento");
  const [danosMorais, setDanosMorais] = useState(false);
  const [aceite, setAceite] = useState(true);
  const [descricao, setDescricao] = useState("Colisão parcial no cruzamento, sem vítimas.");
  const { register, handleSubmit, control, formState } = useForm<DadosSegurado>({
    resolver: zodResolver(esquemaSegurado),
    mode: "onTouched",
    defaultValues: { nome: "Lucas Andrade", email: "lucas@", assistencia: "200" },
  });
  const { errors, isSubmitting } = formState;

  return (
    <form
      noValidate
      onSubmit={(e) => void handleSubmit(() => new Promise((ok) => window.setTimeout(ok, 1200)))(e)}
      className="grid gap-4 md:grid-cols-2"
    >
      <FormField rotulo="Nome do segurado" erro={errors.nome?.message} obrigatorio>
        {(campo) => <Input {...campo} {...register("nome")} autoComplete="name" />}
      </FormField>
      <FormField rotulo="E-mail" erro={errors.email?.message} obrigatorio>
        {(campo) => (
          <Input {...campo} {...register("email")} type="email" icone={Mail} autoComplete="email" />
        )}
      </FormField>
      <FormField
        rotulo="LMI de Danos materiais"
        ajuda={`Entre ${formatarMoeda(LMI_MINIMO)} e ${formatarMoeda(LMI_MAXIMO)}`}
        erro={errors.lmi?.message}
      >
        {(campo) => (
          <Controller
            control={control}
            name="lmi"
            render={({ field }) => (
              <InputMoeda
                {...campo}
                ref={field.ref}
                name={field.name}
                valor={field.value}
                aoMudar={field.onChange}
                onBlur={field.onBlur}
                prefixo="LMI R$"
                placeholder="100.000,00"
              />
            )}
          />
        )}
      </FormField>
      <FormField rotulo="Assistência 24 h" erro={errors.assistencia?.message}>
        {(campo) => (
          <Controller
            control={control}
            name="assistencia"
            render={({ field }) => (
              <Select
                {...campo}
                opcoes={OPCOES_ASSISTENCIA}
                valor={field.value}
                aoMudar={field.onChange}
              />
            )}
          />
        )}
      </FormField>
      <FormField rotulo="Senha" ajuda="Mínimo de 8 caracteres">
        {(campo) => (
          <Input
            {...campo}
            type={senhaVisivel ? "text" : "password"}
            defaultValue="demonstracao"
            sufixo={
              <button
                type="button"
                onClick={() => setSenhaVisivel((v) => !v)}
                aria-label={senhaVisivel ? "Ocultar senha" : "Mostrar senha"}
                className="-mr-2 grid size-9 place-items-center rounded-[6px] text-ink-3 hover:text-ink"
              >
                {senhaVisivel ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
              </button>
            }
          />
        )}
      </FormField>
      <FormField rotulo="Valor do veículo" ajuda="Casco segue o valor do veículo (R$ 141.900,00)">
        {(campo) => <Input {...campo} prefixo="R$" value="141.900,00" disabled readOnly />}
      </FormField>
      <FormField rotulo="Tipo de imóvel">
        {(campo) => (
          <SegmentedControl
            aria-labelledby={campo["aria-labelledby"]}
            opcoes={MOTIVOS_IMOVEL}
            valor={imovel}
            aoMudar={setImovel}
            larguraTotal
          />
        )}
      </FormField>
      <div className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-3">
          <label
            htmlFor={`${id}-casco`}
            className="flex flex-wrap items-center gap-2 text-sm font-semibold text-ink"
          >
            Casco <SeloObrigatoria />
          </label>
          <Switch id={`${id}-casco`} obrigatorio />
        </div>
        <div className="flex items-center justify-between gap-3">
          <label htmlFor={`${id}-morais`} className="text-sm font-semibold text-ink">
            Danos morais{" "}
            <span className="font-normal text-ink-3">
              {danosMorais ? "· R$ 38,00" : "· + R$ 38,00 se ativada"}
            </span>
          </label>
          <Switch id={`${id}-morais`} ligado={danosMorais} aoMudar={setDanosMorais} />
        </div>
      </div>
      <FormField
        rotulo="Descrição do sinistro"
        ajuda="Entre 20 e 2.000 caracteres"
        contador={<ContadorCaracteres atual={descricao.length} maximo={DESCRICAO_MAXIMA} />}
        className="md:col-span-2"
      >
        {(campo) => (
          <Textarea
            {...campo}
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
            maxLength={DESCRICAO_MAXIMA}
          />
        )}
      </FormField>
      <div className="flex flex-col gap-4 md:col-span-2 md:flex-row md:items-center md:justify-between">
        <Checkbox
          rotulo="Manter conectado neste dispositivo"
          marcado={aceite}
          aoMudar={setAceite}
        />
        <Button type="submit" carregando={isSubmitting} rotuloCarregando="Salvando…">
          Salvar segurado
        </Button>
      </div>
    </form>
  );
}

const STATUS_APOLICE: readonly StatusApolice[] = ["Ativa", "Programada", "Vencida", "Cancelada"];
const STATUS_COTACAO: readonly StatusCotacao[] = [
  "Calculada",
  "Aceita",
  "Emitida",
  "Recusada",
  "Expirada",
];
const STATUS_SINISTRO: readonly StatusSinistro[] = [
  "Aberto",
  "EmAnalise",
  "Aprovado",
  "Negado",
  "Pago",
];

export function DemoStatus() {
  return (
    <div className="flex flex-col gap-4">
      <div>
        <Rotulo>Apólice</Rotulo>
        <div className="flex flex-wrap gap-2">
          {STATUS_APOLICE.map((s) => (
            <StatusBadge key={s} entidade="apolice" status={s} />
          ))}
        </div>
      </div>
      <div>
        <Rotulo>Cotação</Rotulo>
        <div className="flex flex-wrap gap-2">
          {STATUS_COTACAO.map((s) => (
            <StatusBadge key={s} entidade="cotacao" status={s} />
          ))}
        </div>
      </div>
      <div>
        <Rotulo>Sinistro</Rotulo>
        <div className="flex flex-wrap gap-2">
          {STATUS_SINISTRO.map((s) => (
            <StatusBadge key={s} entidade="sinistro" status={s} />
          ))}
        </div>
      </div>
      <div>
        <Rotulo>Chips de prazo e contexto</Rotulo>
        <div className="flex flex-wrap gap-2">
          <Badge tom="warn" marcador={Clock}>
            expira em 2 dias
          </Badge>
          <Badge tom="neutral" marcador={Clock}>
            vence em 25 dias
          </Badge>
          <Badge tom="neutral" marcador="plain">
            Obrigatória
          </Badge>
          <Badge tom="neutral" marcador="plain">
            6 de 8 ativas
          </Badge>
        </div>
      </div>
      <div>
        <Rotulo>Produtos</Rotulo>
        <div className="flex flex-wrap items-center gap-4">
          <ProdutoTag produto="AUTO" />
          <ProdutoTag produto="RESID" />
          <ProdutoTag produto="VIDA" comCodigo />
          <ProdutoTag produto="AUTO" variante="chip" />
          <ProdutoTag produto="RESID" variante="chip" />
          <ProdutoTag produto="VIDA" variante="chip" />
        </div>
      </div>
      <div>
        <Rotulo>Avatares</Rotulo>
        <div className="flex flex-wrap items-center gap-2">
          <Avatar nome="Ana Ribeiro" rotulado />
          <Avatar nome="Bruno Lima" variante="steel" rotulado />
          <Avatar nome="Carla Souza" variante="gold" rotulado />
          <Avatar nome="Diego Martins" variante="marinho" rotulado />
          <Avatar nome="Rafael Nogueira Castro" tamanho="sm" rotulado />
          <Avatar nome="Patrícia Menezes" tamanho="lg" variante="steel" rotulado />
        </div>
      </div>
    </div>
  );
}

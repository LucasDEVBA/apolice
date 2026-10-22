import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowLeft, ArrowRight, Check, RotateCcw } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { FormProvider, useForm, useWatch, type Path, type UseFormReturn } from "react-hook-form";
import { useNavigate, useSearchParams } from "react-router";

import { useCriarCotacao, useSimulacaoCotacao } from "@/api/consultas/cotacoes";
import { useProdutos } from "@/api/consultas/produtos";
import type { CotacaoRequest, ProdutoDto, TarifacaoDto } from "@/api/contratos";
import { ErroApi } from "@/api/erros";
import { useUsuario } from "@/app/sessao/sessao-contexto";
import { CarregandoPagina } from "@/app/shell/CarregandoPagina";
import { Pagina } from "@/app/shell/Pagina";
import { Button } from "@/ui/componentes/Button";
import { Card, CardBody, CardFooter, CardHeader } from "@/ui/componentes/Card";
import { notificar } from "@/ui/componentes/notificar";
import type { EstadoCalculo } from "@/ui/componentes/PainelCalculo";
import { StatusRascunho, Stepper, type EtapaStepper } from "@/ui/componentes/Stepper";
import { anunciar } from "@/ui/utils/anunciar";
import { hojeEmBrasilia, idadeEm } from "@/ui/utils/datas";
import { formatarCep } from "@/ui/utils/documentos";
import { formatarHora, formatarMoeda } from "@/ui/utils/formatar";

import { EstadoDaConsulta } from "../comum/EstadoDaConsulta";
import { tratarErroNoFormulario } from "../comum/erros-formulario";
import { useValorComAtraso } from "../comum/useValorComAtraso";
import { EtapaCoberturas } from "./nova/EtapaCoberturas";
import { EtapaProduto } from "./nova/EtapaProduto";
import { EtapaRevisao } from "./nova/EtapaRevisao";
import { EtapaRisco } from "./nova/EtapaRisco";
import { EtapaSegurado } from "./nova/EtapaSegurado";
import {
  criarEsquema,
  mapaDeCamposDaApi,
  paraPedido,
  valoresIniciais,
  type FormularioCotacao,
} from "./nova/modelo";
import { PainelSimulacao, type SimulacaoAoVivo } from "./nova/PainelSimulacao";
import { descartarRascunho, lerRascunho, salvarRascunho } from "./nova/rascunho";

const ATRASO_SIMULACAO_MS = 300;
const ATRASO_RASCUNHO_MS = 600;
const TITULOS = ["Produto", "Segurado", "Risco", "Coberturas", "Revisão"] as const;
const SUBTITULOS = [
  "Escolha o seguro; as coberturas e faixas vêm do catálogo vigente.",
  "Dados de quem contrata. O CPF é validado pelos dígitos verificadores.",
  "As características do risco definem os fatores de preço.",
  "Ative as coberturas e defina o limite máximo de indenização (LMI); o prêmio é recalculado a cada alteração.",
  "Confira tudo antes de criar a cotação. Ela vale por 30 dias.",
] as const;
const ULTIMA_ETAPA = TITULOS.length - 1;
const ID_TITULO_ETAPA = "titulo-etapa";

function camposDaEtapa(etapa: number, v: FormularioCotacao): Path<FormularioCotacao>[] {
  switch (etapa) {
    case 0:
      return ["produto"];
    case 1:
      return ["segurado"];
    case 2:
      return v.produto === "AUTO"
        ? ["auto"]
        : v.produto === "RESID"
          ? ["resid"]
          : ["vida", "segurado.dataNascimento"];
    case 3:
      return ["coberturas"];
    default:
      return [];
  }
}

/** Etapa onde mora um campo do formulário (para levar o usuário ao erro devolvido pela API). */
function etapaDoCampo(campo: string): number {
  if (campo.startsWith("segurado")) return 1;
  if (campo.startsWith("coberturas")) return 3;
  return 2;
}

function resumoRisco(v: FormularioCotacao): string | undefined {
  if (v.produto === "AUTO" && v.auto.valorVeiculo)
    return `${formatarMoeda(v.auto.valorVeiculo)} · ${v.auto.anoFabricacao}`;
  if (v.produto === "RESID" && v.resid.valorImovel)
    return `${v.resid.tipoImovel} · ${formatarCep(v.resid.cep)}`;
  if (v.produto === "VIDA") return v.vida.fumante ? "Fumante" : "Não fumante";
  return undefined;
}

function etapasDoStepper(v: FormularioCotacao, produto: ProdutoDto | undefined): EtapaStepper[] {
  const idade = /^\d{4}-\d{2}-\d{2}$/.test(v.segurado.dataNascimento)
    ? ` · ${String(idadeEm(v.segurado.dataNascimento, hojeEmBrasilia()))} anos`
    : "";
  const ativas = produto?.coberturas.filter(
    (c) => c.obrigatoria || v.coberturas[c.codigo]?.ativa,
  ).length;
  const resumos = [
    produto?.nome,
    v.segurado.nome ? `${v.segurado.nome.split(" ")[0] ?? ""}${idade}` : undefined,
    resumoRisco(v),
    ativas === undefined
      ? undefined
      : `${String(ativas)} de ${String(produto?.coberturas.length ?? 0)} ativas`,
    "Conferência e criação",
  ];
  return TITULOS.map((titulo, i) => ({
    id: titulo,
    titulo,
    href: `?etapa=${String(i + 1)}`,
    ...(resumos[i] ? { resumo: resumos[i] } : {}),
  }));
}

interface Ultima {
  produto: string;
  atual: TarifacaoDto;
  anterior: TarifacaoDto | undefined;
  em: number;
}

/** Simulação ao vivo com debounce de 300 ms; o último resultado válido nunca some da tela. */
function useSimulacaoAoVivo(pedido: CotacaoRequest | null, produtoCodigo: string): SimulacaoAoVivo {
  const chave = pedido ? JSON.stringify(pedido) : null;
  const chaveAtrasada = useValorComAtraso(chave, ATRASO_SIMULACAO_MS);
  const pedidoAtrasado = useMemo(
    () => (chaveAtrasada ? (JSON.parse(chaveAtrasada) as CotacaoRequest) : null),
    [chaveAtrasada],
  );
  const consulta = useSimulacaoCotacao(pedidoAtrasado);
  const [ultima, setUltima] = useState<Ultima | null>(null);
  const valida = consulta.data && !consulta.isPlaceholderData ? consulta.data : undefined;
  if (valida && valida !== ultima?.atual) {
    setUltima({
      produto: produtoCodigo,
      atual: valida,
      anterior: ultima?.produto === produtoCodigo ? ultima.atual : undefined,
      em: consulta.dataUpdatedAt,
    });
  }
  const mesmaEscolha = ultima?.produto === produtoCodigo ? ultima : null;
  const estado: EstadoCalculo =
    consulta.isError || (pedido === null && mesmaEscolha !== null)
      ? "desatualizado"
      : consulta.isFetching || chave !== chaveAtrasada
        ? "recalculando"
        : "atualizado";
  return {
    tarifacao: mesmaEscolha?.atual,
    anterior: mesmaEscolha?.anterior,
    estado,
    erro: consulta.error,
    atualizadoEm: mesmaEscolha?.em,
    tentarDeNovo: () => void consulta.refetch(),
  };
}

interface Autosave {
  salvoEm: string | undefined;
  /** Cotação criada: cancela o salvamento pendente para o rascunho não renascer. */
  encerrar: () => void;
}

/** Autosave do rascunho do usuário em sessionStorage, 600 ms depois da última alteração. */
function useAutosave(usuarioId: string, valores: FormularioCotacao, etapaMaxima: number): Autosave {
  const [salvoEm, setSalvoEm] = useState<string | undefined>(() => lerRascunho(usuarioId)?.salvoEm);
  const encerrado = useRef(false);
  const texto = JSON.stringify(valores);
  useEffect(() => {
    if (!(JSON.parse(texto) as FormularioCotacao).produto) return;
    const timer = window.setTimeout(() => {
      // A tela ainda fica montada enquanto a rota da cotação criada carrega (transição).
      if (encerrado.current) return;
      const agora = new Date().toISOString();
      const ok = salvarRascunho(usuarioId, {
        valores: JSON.parse(texto) as FormularioCotacao,
        etapaMaxima,
        salvoEm: agora,
      });
      if (ok) setSalvoEm(agora);
    }, ATRASO_RASCUNHO_MS);
    return () => window.clearTimeout(timer);
  }, [usuarioId, texto, etapaMaxima]);
  return {
    salvoEm,
    encerrar: () => {
      encerrado.current = true;
      descartarRascunho(usuarioId);
    },
  };
}

/**
 * Campo com erro revalida a cada tecla, mesmo antes do primeiro blur. Sem isso, o erro do campo
 * corrigido (ainda focado) só sumia no blur provocado pelo clique em "Continuar": o rodapé subia
 * entre o mousedown e o mouseup e o clique se perdia.
 */
function useRevalidarCamposComErro(form: UseFormReturn<FormularioCotacao>) {
  useEffect(() => {
    const assinatura = form.watch((_, { name, type }) => {
      if (type !== "change" || !name || !form.getFieldState(name).error) return;
      void form.trigger(name);
    });
    return () => assinatura.unsubscribe();
  }, [form]);
}

/**
 * Troca de etapa (WCAG 2.4.3 e 4.1.3): o botão ou o campo que estava focado some, então o foco vai
 * para o título da etapa nova, e a região polite diz em que ponto do assistente o usuário está.
 */
function useFocoNaTrocaDeEtapa(etapa: number) {
  const anterior = useRef(etapa);
  useEffect(() => {
    if (anterior.current === etapa) return;
    anterior.current = etapa;
    document.getElementById(ID_TITULO_ETAPA)?.focus();
    anunciar(`Etapa ${String(etapa + 1)} de ${String(TITULOS.length)}: ${TITULOS[etapa] ?? ""}`);
  }, [etapa]);
}

function Assistente({ produtos }: { produtos: readonly ProdutoDto[] }) {
  const navegar = useNavigate();
  const usuario = useUsuario();
  const [params, setParams] = useSearchParams();
  const [rascunho] = useState(() => lerRascunho(usuario.id));
  const hoje = hojeEmBrasilia();
  const esquema = useMemo(() => criarEsquema(produtos, hoje), [produtos, hoje]);
  const form = useForm<FormularioCotacao>({
    resolver: zodResolver(esquema),
    defaultValues: rascunho?.valores ?? valoresIniciais(),
    mode: "onTouched",
    reValidateMode: "onChange",
  });
  useRevalidarCamposComErro(form);
  const [etapaMaxima, setEtapaMaxima] = useState(rascunho?.etapaMaxima ?? 0);
  const etapa = Math.min(Math.max(Number(params.get("etapa") ?? "1") - 1, 0), etapaMaxima);
  const valores = useWatch({ control: form.control }) as FormularioCotacao;
  const produto = produtos.find((p) => p.codigo === valores.produto);
  const pedido = useMemo(() => {
    if (!produto) return null;
    const resultado = esquema.safeParse(valores);
    return resultado.success ? paraPedido(resultado.data, produto) : null;
  }, [esquema, valores, produto]);
  const simulacao = useSimulacaoAoVivo(pedido, valores.produto);
  const [parcelas, setParcelas] = useState(10);
  const autosave = useAutosave(usuario.id, valores, etapaMaxima);
  useFocoNaTrocaDeEtapa(etapa);
  const criar = useCriarCotacao();

  function irPara(indice: number) {
    setParams({ etapa: String(indice + 1) });
  }

  async function avancar() {
    const ok = await form.trigger(camposDaEtapa(etapa, valores), { shouldFocus: true });
    if (!ok) return;
    if (
      etapa === 2 &&
      valores.produto === "RESID" &&
      valores.coberturas.INCENDIO?.lmi === undefined
    ) {
      form.setValue("coberturas.INCENDIO.lmi", valores.resid.valorImovel);
    }
    const proxima = Math.min(etapa + 1, ULTIMA_ETAPA);
    setEtapaMaxima((m) => Math.max(m, proxima));
    irPara(proxima);
  }

  async function enviar(v: FormularioCotacao) {
    if (!produto) return;
    try {
      const cotacao = await criar.mutateAsync(paraPedido(v, produto));
      autosave.encerrar();
      notificar.sucesso({
        titulo: "Cotação criada",
        descricao: `Prêmio total de ${formatarMoeda(cotacao.premioTotal)}, válida por 30 dias.`,
      });
      void navegar(`/cotacoes/${cotacao.id}`, { replace: true });
    } catch (erro) {
      const mapa = mapaDeCamposDaApi(v.produto) as Record<string, Path<FormularioCotacao>>;
      tratarErroNoFormulario(erro, form.setError, mapa);
      const primeiro = erro instanceof ErroApi ? Object.keys(erro.errosPorCampo)[0] : undefined;
      const campo = primeiro ? mapa[primeiro] : undefined;
      if (campo) irPara(etapaDoCampo(campo));
    }
  }

  function recomecar() {
    descartarRascunho(usuario.id);
    form.reset(valoresIniciais());
    setEtapaMaxima(0);
    irPara(0);
  }

  const etapas = etapasDoStepper(valores, produto);
  const conteudo = [
    <EtapaProduto key="produto" produtos={produtos} />,
    <EtapaSegurado key="segurado" />,
    <EtapaRisco key="risco" />,
    produto ? (
      <EtapaCoberturas key="coberturas" produto={produto} tarifacao={simulacao.tarifacao} />
    ) : null,
    produto ? (
      <EtapaRevisao key="revisao" produto={produto} tarifacao={simulacao.tarifacao} />
    ) : null,
  ][etapa];

  return (
    <Pagina
      titulo={produto ? `Nova cotação · ${produto.nome}` : "Nova cotação"}
      migalhas={[{ rotulo: "Cotações", href: "/cotacoes" }, { rotulo: "Nova cotação" }]}
      acoes={
        <>
          <StatusRascunho salvoAs={autosave.salvoEm ? formatarHora(autosave.salvoEm) : undefined} />
          {valores.produto && (
            <Button variante="fantasma" tamanho="sm" icone={RotateCcw} onClick={recomecar}>
              Recomeçar
            </Button>
          )}
        </>
      }
    >
      <Stepper etapas={etapas} atual={etapa} />
      <div className="grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-[minmax(0,1fr)_376px] lg:items-start">
        <FormProvider {...form}>
          <form
            noValidate
            aria-labelledby={ID_TITULO_ETAPA}
            onSubmit={(e) => {
              e.preventDefault();
              if (etapa === ULTIMA_ETAPA) void form.handleSubmit(enviar)(e);
              else void avancar();
            }}
          >
            <Card>
              <CardHeader
                id={ID_TITULO_ETAPA}
                focavel
                titulo={TITULOS[etapa] ?? ""}
                subtitulo={SUBTITULOS[etapa]}
              />
              <CardBody>{conteudo}</CardBody>
              <CardFooter className="justify-between">
                {etapa > 0 ? (
                  <Button variante="fantasma" icone={ArrowLeft} onClick={() => irPara(etapa - 1)}>
                    Voltar para {TITULOS[etapa - 1]}
                  </Button>
                ) : (
                  <span />
                )}
                {etapa === ULTIMA_ETAPA ? (
                  <Button
                    type="submit"
                    icone={Check}
                    carregando={criar.isPending}
                    rotuloCarregando="Criando…"
                    desabilitado={!simulacao.tarifacao}
                  >
                    Criar cotação
                  </Button>
                ) : (
                  <Button type="submit" iconeDireita={ArrowRight}>
                    {etapa === ULTIMA_ETAPA - 1
                      ? "Revisar cotação"
                      : `Continuar para ${TITULOS[etapa + 1] ?? ""}`}
                  </Button>
                )}
              </CardFooter>
            </Card>
          </form>
        </FormProvider>
        <aside aria-label="Cálculo do prêmio">
          <PainelSimulacao
            produto={produto}
            simulacao={simulacao}
            parcelas={parcelas}
            aoMudarParcelas={setParcelas}
          />
        </aside>
      </div>
    </Pagina>
  );
}

export default function NovaCotacao() {
  const produtos = useProdutos();
  if (produtos.isPending) return <CarregandoPagina />;
  if (produtos.isError) {
    return (
      <Pagina
        titulo="Nova cotação"
        migalhas={[{ rotulo: "Cotações", href: "/cotacoes" }, { rotulo: "Nova cotação" }]}
      >
        <EstadoDaConsulta
          erro={produtos.error}
          entidade="o catálogo de produtos"
          voltarPara={{ rotulo: "Voltar às cotações", href: "/cotacoes" }}
          aoTentarDeNovo={() => void produtos.refetch()}
        />
      </Pagina>
    );
  }
  return <Assistente produtos={produtos.data} />;
}

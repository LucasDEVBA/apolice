import { FilePen } from "lucide-react";
import { useMemo, useState } from "react";

import { useEndossarApolice, useSimulacaoEndosso } from "@/api/consultas/apolices";
import { useProduto } from "@/api/consultas/produtos";
import type {
  ApoliceDto,
  CoberturaProdutoDto,
  EndossoRequest,
  ProdutoDto,
  TipoEndosso,
} from "@/api/contratos";
import { mensagemDoErro } from "@/api/erros";
import { Button } from "@/ui/componentes/Button";
import { GrupoCartoesEscolha } from "@/ui/componentes/CartaoEscolha";
import { Dialog } from "@/ui/componentes/Dialog";
import { FormField } from "@/ui/componentes/FormField";
import { InputMoeda } from "@/ui/componentes/InputMoeda";
import { notificar } from "@/ui/componentes/notificar";
import { Select } from "@/ui/componentes/Select";
import { RegiaoCarregando, SkeletonTexto } from "@/ui/componentes/Skeleton";
import { Money } from "@/ui/componentes/Valores";
import { formatarData, formatarMoeda, formatarNumero } from "@/ui/utils/formatar";

import { RodapeDoDialogo } from "../comum/RodapeDoDialogo";
import { ROTULO_TIPO_ENDOSSO } from "../comum/rotulos";
import { useValorComAtraso } from "../comum/useValorComAtraso";
import { lmiVariavel } from "../cotacoes/nova/modelo";

const TIPOS: readonly TipoEndosso[] = ["AlteracaoLmi", "InclusaoCobertura", "ExclusaoCobertura"];
const DESCRICAO_TIPO: Record<TipoEndosso, string> = {
  AlteracaoLmi: "Muda o limite de uma cobertura",
  InclusaoCobertura: "Acrescenta uma cobertura do catálogo",
  ExclusaoCobertura: "Retira uma cobertura opcional",
};
const ATRASO_SIMULACAO_MS = 300;

/** Coberturas elegíveis para cada tipo de endosso (SPEC §4.4). */
function elegiveis(
  tipo: TipoEndosso,
  apolice: ApoliceDto,
  produto: ProdutoDto,
): CoberturaProdutoDto[] {
  const contratada = (codigo: string) => apolice.coberturas.some((c) => c.codigo === codigo);
  return produto.coberturas.filter((c) => {
    if (tipo === "InclusaoCobertura") return !contratada(c.codigo);
    if (tipo === "ExclusaoCobertura") return contratada(c.codigo) && !c.obrigatoria;
    return contratada(c.codigo) && lmiVariavel(c, produto.codigo);
  });
}

function pedidoValido(
  tipo: TipoEndosso,
  cobertura: CoberturaProdutoDto | undefined,
  lmi: number | undefined,
  produto: ProdutoDto,
  lmiAtual: number | undefined,
): EndossoRequest | null {
  if (!cobertura) return null;
  const precisaLmi = tipo !== "ExclusaoCobertura" && lmiVariavel(cobertura, produto.codigo);
  if (!precisaLmi) return { tipo, coberturaCodigo: cobertura.codigo, lmi: null };
  if (
    lmi === undefined ||
    Number.isNaN(lmi) ||
    lmi < cobertura.lmiMinimo ||
    lmi > cobertura.lmiMaximo
  )
    return null;
  // Alterar para o mesmo LMI não é endosso: não há o que simular.
  if (tipo === "AlteracaoLmi" && lmi === lmiAtual) return null;
  return { tipo, coberturaCodigo: cobertura.codigo, lmi };
}

function ResultadoSimulacao({
  apolice,
  pedido,
}: {
  apolice: ApoliceDto;
  pedido: EndossoRequest | null;
}) {
  const simulacao = useSimulacaoEndosso(apolice.id, pedido);
  if (!pedido)
    return (
      <p className="text-sm text-ink-3">
        Escolha a cobertura (e o novo LMI) para ver o ajuste pro rata.
      </p>
    );
  if (simulacao.isError) {
    const { titulo, descricao } = mensagemDoErro(simulacao.error);
    return (
      <p
        role="alert"
        className="rounded-lg border border-bad-line bg-bad-bg px-3.5 py-2.5 text-label text-bad"
      >
        <strong>{titulo}.</strong> {descricao}
      </p>
    );
  }
  const dados = simulacao.data;
  return (
    <RegiaoCarregando carregando={simulacao.isPending} rotulo="Calculando o ajuste…">
      {dados ? (
        <div className="grid gap-4 sm:grid-cols-[1fr_auto] sm:items-end">
          <dl className="text-sm">
            {[
              ["Efeito a partir de", formatarData(dados.dataEfeito)],
              [
                "Dias restantes",
                `${formatarNumero(dados.diasRestantes)} de ${formatarNumero(dados.diasVigencia)}`,
              ],
              [
                "Prêmio anual do item",
                `${formatarMoeda(dados.premioAnualAnterior)} → ${formatarMoeda(dados.premioAnualNovo)}`,
              ],
              ["Ajuste líquido (pro rata)", formatarMoeda(dados.ajusteLiquido)],
              ["IOF do ajuste", formatarMoeda(dados.iofAjuste)],
            ].map(([rotulo, valor]) => (
              <div key={rotulo} className="flex items-baseline justify-between gap-4 py-1.5">
                <dt className="text-label text-ink-2">{rotulo}</dt>
                <dd className="text-ink tnum">{valor}</dd>
              </div>
            ))}
          </dl>
          <div className="rounded-lg bg-surface-2 px-4 py-3 sm:text-right" aria-live="polite">
            <p className="text-label text-ink-2">
              {dados.ajusteTotal < 0 ? "A devolver" : "A cobrar"}
            </p>
            <Money
              valor={dados.ajusteTotal}
              variante="heroi"
              className="block type-fig-lg text-ink"
            />
          </div>
        </div>
      ) : (
        <SkeletonTexto linhas={4} />
      )}
    </RegiaoCarregando>
  );
}

function Formulario({
  apolice,
  produto,
  aoFechar,
}: {
  apolice: ApoliceDto;
  produto: ProdutoDto;
  aoFechar: () => void;
}) {
  const [tipo, setTipo] = useState<TipoEndosso>("AlteracaoLmi");
  const [codigo, setCodigo] = useState("");
  const [lmi, setLmi] = useState<number | undefined>(undefined);
  const endossar = useEndossarApolice(apolice.id);
  const opcoes = elegiveis(tipo, apolice, produto);
  const cobertura = opcoes.find((c) => c.codigo === codigo);
  const lmiAtual = apolice.coberturas.find((c) => c.codigo === codigo)?.lmi;
  const pedido = pedidoValido(tipo, cobertura, lmi, produto, lmiAtual);
  const chave = pedido ? JSON.stringify(pedido) : null;
  const chaveAtrasada = useValorComAtraso(chave, ATRASO_SIMULACAO_MS);
  const pedidoAtrasado = useMemo(
    () => (chaveAtrasada ? (JSON.parse(chaveAtrasada) as EndossoRequest) : null),
    [chaveAtrasada],
  );
  const simulacao = useSimulacaoEndosso(apolice.id, pedidoAtrasado);
  const precisaLmi =
    cobertura !== undefined &&
    tipo !== "ExclusaoCobertura" &&
    lmiVariavel(cobertura, produto.codigo);
  const erroLmi =
    precisaLmi &&
    lmi !== undefined &&
    (Number.isNaN(lmi) || lmi < cobertura.lmiMinimo || lmi > cobertura.lmiMaximo)
      ? `Informe um LMI entre ${formatarMoeda(cobertura.lmiMinimo)} e ${formatarMoeda(cobertura.lmiMaximo)}.`
      : tipo === "AlteracaoLmi" && lmi !== undefined && lmi === lmiAtual
        ? "Informe um LMI diferente do atual."
        : undefined;

  async function efetivar() {
    if (!pedido) return;
    try {
      const atualizada = await endossar.mutateAsync(pedido);
      const numero = atualizada.endossos.at(-1)?.numero;
      notificar.sucesso({
        titulo: `Endosso nº ${String(numero ?? "")} registrado`,
        descricao: `${ROTULO_TIPO_ENDOSSO[pedido.tipo]} em ${atualizada.numero}.`,
      });
      aoFechar();
    } catch {
      // O MutationCache mostra o toast com o correlationId.
    }
  }

  return (
    <>
      <div className="flex flex-col gap-5">
        <fieldset>
          <legend className="mb-2 type-label text-ink">Tipo de endosso</legend>
          <GrupoCartoesEscolha<TipoEndosso>
            rotulo="Tipo de endosso"
            colunas={3}
            valor={tipo}
            aoMudar={(novo) => {
              setTipo(novo);
              setCodigo("");
              setLmi(undefined);
            }}
            opcoes={TIPOS.map((t) => ({
              valor: t,
              titulo: ROTULO_TIPO_ENDOSSO[t],
              descricao: DESCRICAO_TIPO[t],
              ...(elegiveis(t, apolice, produto).length === 0
                ? { indisponivel: "Nenhuma cobertura elegível" }
                : {}),
            }))}
          />
        </fieldset>
        <div className="grid gap-5 md:grid-cols-2">
          <FormField
            rotulo="Cobertura"
            obrigatorio
            ajuda={
              lmiAtual !== undefined && tipo === "AlteracaoLmi"
                ? `LMI atual: ${formatarMoeda(lmiAtual)}`
                : undefined
            }
          >
            {(campo) => (
              <Select
                {...campo}
                placeholder="Escolha a cobertura"
                opcoes={opcoes.map((c) => ({ valor: c.codigo, rotulo: c.nome }))}
                valor={codigo || undefined}
                aoMudar={(novo) => {
                  setCodigo(novo);
                  const escolhida = opcoes.find((c) => c.codigo === novo);
                  const atual = apolice.coberturas.find((c) => c.codigo === novo)?.lmi;
                  setLmi(atual ?? escolhida?.lmiMinimo);
                }}
              />
            )}
          </FormField>
          {precisaLmi && (
            <FormField
              rotulo="Novo LMI"
              obrigatorio
              erro={erroLmi}
              ajuda={`Entre ${formatarMoeda(cobertura.lmiMinimo)} e ${formatarMoeda(cobertura.lmiMaximo)}.`}
            >
              {(campo) => <InputMoeda {...campo} prefixo="LMI R$" valor={lmi} aoMudar={setLmi} />}
            </FormField>
          )}
        </div>
        <section aria-label="Simulação do endosso" className="rounded-xl border border-line p-4">
          <ResultadoSimulacao apolice={apolice} pedido={pedidoAtrasado} />
        </section>
      </div>
      <RodapeDoDialogo>
        <Button variante="secundario" onClick={aoFechar} desabilitado={endossar.isPending}>
          Voltar
        </Button>
        <Button
          icone={FilePen}
          desabilitado={!pedido || chave !== chaveAtrasada || !simulacao.data}
          carregando={endossar.isPending}
          rotuloCarregando="Endossando…"
          onClick={() => void efetivar()}
        >
          Efetivar endosso
        </Button>
      </RodapeDoDialogo>
    </>
  );
}

interface DialogoEndossoProps {
  apolice: ApoliceDto;
  aberto: boolean;
  aoMudar: (aberto: boolean) => void;
}

/** Endosso com simulação do ajuste pro rata antes de efetivar (SPEC §4.4, DESIGN.md §4.17). */
export function DialogoEndosso({ apolice, aberto, aoMudar }: DialogoEndossoProps) {
  const produto = useProduto(apolice.produtoCodigo);
  return (
    <Dialog
      aberto={aberto}
      aoMudar={aoMudar}
      titulo={`Endossar a apólice ${apolice.numero}`}
      descricao="O ajuste é proporcional aos dias que faltam na vigência, com o IOF do produto."
      largura="simulacao"
    >
      {produto ? (
        <Formulario apolice={apolice} produto={produto} aoFechar={() => aoMudar(false)} />
      ) : (
        <SkeletonTexto linhas={4} />
      )}
    </Dialog>
  );
}

import { Ban, Info } from "lucide-react";
import { useState } from "react";

import { useCancelarApolice, useSimulacaoCancelamento } from "@/api/consultas/apolices";
import type { ApoliceDto, MotivoCancelamento } from "@/api/contratos";
import { mensagemDoErro } from "@/api/erros";
import { Button } from "@/ui/componentes/Button";
import { GrupoCartoesEscolha } from "@/ui/componentes/CartaoEscolha";
import { Dialog } from "@/ui/componentes/Dialog";
import { notificar } from "@/ui/componentes/notificar";
import { RegiaoCarregando, SkeletonTexto } from "@/ui/componentes/Skeleton";
import { Money } from "@/ui/componentes/Valores";
import {
  formatarData,
  formatarMoeda,
  formatarNumero,
  formatarPercentual,
} from "@/ui/utils/formatar";

import { RodapeDoDialogo } from "../comum/RodapeDoDialogo";
import {
  DESCRICAO_MOTIVO_CANCELAMENTO,
  ROTULO_METODO_CANCELAMENTO,
  ROTULO_MOTIVO_CANCELAMENTO,
} from "../comum/rotulos";

const MOTIVOS: readonly MotivoCancelamento[] = [
  "PedidoDoSegurado",
  "IniciativaSeguradora",
  "Inadimplencia",
];

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4 py-1.5">
      <dt className="text-label text-ink-2">{rotulo}</dt>
      <dd className="text-sm text-ink tnum">{valor}</dd>
    </div>
  );
}

function Simulacao({
  apolice,
  motivo,
}: {
  apolice: ApoliceDto;
  motivo: MotivoCancelamento | null;
}) {
  const simulacao = useSimulacaoCancelamento(apolice.id, motivo);
  if (!motivo) {
    return <p className="text-sm text-ink-3">Escolha o motivo para ver a restituição calculada.</p>;
  }
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
    <RegiaoCarregando carregando={simulacao.isPending} rotulo="Calculando a restituição…">
      {dados ? (
        <div className="grid gap-4 sm:grid-cols-[1fr_auto] sm:items-end">
          <dl>
            <Linha rotulo="Método" valor={ROTULO_METODO_CANCELAMENTO[dados.metodo]} />
            <Linha rotulo="Data do cancelamento" valor={formatarData(dados.dataCancelamento)} />
            <Linha
              rotulo="Dias decorridos"
              valor={`${formatarNumero(dados.diasDecorridos)} de ${formatarNumero(dados.diasVigencia)}`}
            />
            {dados.percentualRetido !== null && (
              <Linha
                rotulo="Retido (tabela de prazo curto)"
                valor={formatarPercentual(dados.percentualRetido, 0)}
              />
            )}
            <Linha rotulo="Prêmio líquido vigente" valor={formatarMoeda(dados.premioLiquidoBase)} />
          </dl>
          <div className="rounded-lg bg-surface-2 px-4 py-3 sm:text-right" aria-live="polite">
            <p className="text-label text-ink-2">Restituição</p>
            <Money
              valor={dados.valorRestituicao}
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

interface DialogoCancelamentoProps {
  apolice: ApoliceDto;
  aberto: boolean;
  aoMudar: (aberto: boolean) => void;
}

/** Cancelamento com motivo em cartões, simulação ao vivo da restituição e confirmação (DESIGN.md §4.17). */
export function DialogoCancelamento({ apolice, aberto, aoMudar }: DialogoCancelamentoProps) {
  const [motivo, setMotivo] = useState<MotivoCancelamento | null>(null);
  const cancelar = useCancelarApolice(apolice.id);
  const simulacao = useSimulacaoCancelamento(apolice.id, motivo);

  function mudar(novo: boolean) {
    if (cancelar.isPending) return;
    if (!novo) setMotivo(null);
    aoMudar(novo);
  }

  async function confirmar() {
    if (!motivo) return;
    try {
      const atualizada = await cancelar.mutateAsync(motivo);
      notificar.sucesso({
        titulo: `Apólice ${atualizada.numero} cancelada`,
        descricao:
          atualizada.valorRestituicao === null
            ? undefined
            : `Restituição de ${formatarMoeda(atualizada.valorRestituicao)}.`,
      });
      mudar(false);
    } catch {
      // O toast com o correlationId vem do MutationCache; o diálogo fica aberto.
    }
  }

  return (
    <Dialog
      aberto={aberto}
      aoMudar={mudar}
      titulo={`Cancelar a apólice ${apolice.numero}?`}
      descricao="O cancelamento encerra a cobertura hoje e não pode ser desfeito."
      largura="simulacao"
    >
      <div className="flex flex-col gap-5">
        <fieldset>
          <legend className="mb-2 type-label text-ink">Motivo</legend>
          <GrupoCartoesEscolha<MotivoCancelamento>
            rotulo="Motivo do cancelamento"
            colunas={3}
            valor={motivo ?? ""}
            aoMudar={setMotivo}
            opcoes={MOTIVOS.map((m) => ({
              valor: m,
              titulo: ROTULO_MOTIVO_CANCELAMENTO[m],
              descricao: DESCRICAO_MOTIVO_CANCELAMENTO[m],
            }))}
          />
        </fieldset>
        <section
          aria-label="Simulação da restituição"
          className="rounded-xl border border-line p-4"
        >
          <Simulacao apolice={apolice} motivo={motivo} />
        </section>
        <p className="flex items-start gap-2 type-meta text-ink-3">
          <Info size={15} className="mt-px shrink-0" aria-hidden />O IOF não é restituído.
        </p>
      </div>
      <RodapeDoDialogo>
        <Button
          variante="secundario"
          onClick={() => mudar(false)}
          desabilitado={cancelar.isPending}
        >
          Voltar
        </Button>
        <Button
          variante="perigo"
          icone={Ban}
          desabilitado={!motivo || !simulacao.data}
          carregando={cancelar.isPending}
          rotuloCarregando="Cancelando…"
          onClick={() => void confirmar()}
        >
          Cancelar apólice
        </Button>
      </RodapeDoDialogo>
    </Dialog>
  );
}

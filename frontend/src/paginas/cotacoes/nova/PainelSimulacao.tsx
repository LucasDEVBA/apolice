import { Calculator, TriangleAlert } from "lucide-react";
import { useState, type ReactNode } from "react";

import type { ProdutoDto, TarifacaoDto } from "@/api/contratos";
import { mensagemDoErro } from "@/api/erros";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { Dialog } from "@/ui/componentes/Dialog";
import {
  BarraTotalMobile,
  PainelCalculo,
  type EstadoCalculo,
} from "@/ui/componentes/PainelCalculo";
import { hojeEmBrasilia, somarDias } from "@/ui/utils/datas";

import { fatoresDoCalculo, itensDoCalculo } from "./calculo";

const DIAS_VALIDADE = 30;

export interface SimulacaoAoVivo {
  /** Último resultado válido (continua na tela se a simulação seguinte falhar). */
  tarifacao: TarifacaoDto | undefined;
  anterior: TarifacaoDto | undefined;
  estado: EstadoCalculo;
  erro: unknown;
  atualizadoEm: number | undefined;
  tentarDeNovo: () => void;
}

interface PainelSimulacaoProps {
  produto: ProdutoDto | undefined;
  simulacao: SimulacaoAoVivo;
  parcelas: number;
  aoMudarParcelas: (parcelas: number) => void;
  acao?: ReactNode;
}

function AvisoSemCalculo() {
  return (
    <Card className="lg:sticky lg:top-[calc(var(--topbar-h)+24px)]">
      <CardHeader titulo="Cálculo do prêmio" />
      <CardBody className="flex items-start gap-3 text-sm text-ink-2">
        <span className="grid size-10 shrink-0 place-items-center rounded-lg border border-line bg-surface-2 text-ink-2">
          <Calculator size={18} aria-hidden />
        </span>
        <p>
          O prêmio aparece aqui, ao vivo, assim que produto, segurado e dados do risco estiverem
          completos. Cada alteração recalcula em instantes.
        </p>
      </CardBody>
    </Card>
  );
}

function painel(props: PainelSimulacaoProps, tarifacao: TarifacaoDto, produto: ProdutoDto) {
  const { simulacao, parcelas, aoMudarParcelas, acao } = props;
  return (
    <PainelCalculo
      tarifa={`Tarifa ${produto.nome}`}
      referencia={hojeEmBrasilia()}
      atualizadoEm={simulacao.atualizadoEm ? new Date(simulacao.atualizadoEm) : undefined}
      itens={itensDoCalculo(tarifacao, produto, simulacao.anterior)}
      fatores={fatoresDoCalculo(tarifacao, produto)}
      premioLiquido={tarifacao.premioLiquido}
      aliquotaIof={tarifacao.aliquotaIof}
      iof={tarifacao.iof}
      premioTotal={tarifacao.premioTotal}
      maxParcelas={tarifacao.maxParcelas}
      parcelas={Math.min(parcelas, tarifacao.maxParcelas)}
      aoMudarParcelas={aoMudarParcelas}
      validaAte={somarDias(hojeEmBrasilia(), DIAS_VALIDADE)}
      estado={simulacao.estado}
      aoTentarDeNovo={simulacao.tentarDeNovo}
      {...(acao ? { acao } : {})}
    />
  );
}

function AvisoErro({ erro }: { erro: unknown }) {
  const { titulo, descricao } = mensagemDoErro(erro);
  return (
    <p
      role="alert"
      className="mt-3 flex items-start gap-2 rounded-lg border border-warn-line bg-warn-bg px-3.5 py-2.5 text-label text-warn"
    >
      <TriangleAlert size={15} className="mt-px shrink-0" aria-hidden />
      <span>
        <span className="font-semibold">{titulo}.</span> {descricao}
      </span>
    </p>
  );
}

/**
 * Painel de cálculo ao vivo (DESIGN.md §4.12): coluna fixa no lg+; abaixo disso, barra inferior de
 * 64 px com o total que abre a memória completa num bottom sheet.
 */
export function PainelSimulacao(props: PainelSimulacaoProps) {
  const [folhaAberta, setFolhaAberta] = useState(false);
  const { produto, simulacao } = props;
  const tarifacao = simulacao.tarifacao;
  if (!produto || !tarifacao) {
    return (
      <div className="max-lg:hidden">
        <AvisoSemCalculo />
        {simulacao.estado === "desatualizado" && <AvisoErro erro={simulacao.erro} />}
      </div>
    );
  }
  return (
    <>
      <div className="max-lg:hidden lg:sticky lg:top-[calc(var(--topbar-h)+24px)]">
        {painel(props, tarifacao, produto)}
        {simulacao.estado === "desatualizado" && <AvisoErro erro={simulacao.erro} />}
      </div>
      <div className="lg:hidden">
        <BarraTotalMobile
          premioTotal={tarifacao.premioTotal}
          aoVerCalculo={() => setFolhaAberta(true)}
        />
        <Dialog
          aberto={folhaAberta}
          aoMudar={setFolhaAberta}
          titulo="Memória de cálculo"
          largura="simulacao"
        >
          {painel({ ...props, acao: undefined }, tarifacao, produto)}
          {simulacao.estado === "desatualizado" && <AvisoErro erro={simulacao.erro} />}
        </Dialog>
      </div>
    </>
  );
}

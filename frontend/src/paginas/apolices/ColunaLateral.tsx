import { Link } from "react-router";

import type { ApoliceDto } from "@/api/contratos";
import { Avatar } from "@/ui/componentes/Avatar";
import { StatusBadge } from "@/ui/componentes/Badge";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { Timeline, type EventoTimeline } from "@/ui/componentes/Timeline";
import { Gravura } from "@/ui/marca/Marca";
import { hojeEmBrasilia, idadeEm } from "@/ui/utils/datas";
import { formatarMoeda } from "@/ui/utils/formatar";

import { ROTULO_MOTIVO_CANCELAMENTO, ROTULO_TIPO_ENDOSSO } from "../comum/rotulos";

function eventosDa(apolice: ApoliceDto): EventoTimeline[] {
  const eventos: EventoTimeline[] = [
    {
      id: "emissao",
      quando: apolice.emitidaEm,
      tom: "navy",
      titulo: "Apólice emitida",
      descricao: `Por ${apolice.corretorNome}, em ${String(apolice.parcelas)}× · ${formatarMoeda(apolice.premioTotal)}`,
    },
    {
      id: "inicio",
      quando: apolice.vigenciaInicio,
      tom: "navy",
      titulo: "Início da vigência",
    },
    {
      id: "fim",
      quando: apolice.vigenciaFim,
      tom: "navy",
      titulo: "Fim da vigência",
    },
    ...apolice.endossos.map<EventoTimeline>((e) => ({
      id: `endosso-${e.id}`,
      quando: e.criadoEm,
      tom: "gold",
      titulo: `Endosso nº ${String(e.numero)} emitido`,
      descricao: `${ROTULO_TIPO_ENDOSSO[e.tipo]} · ${e.coberturaNome} · ajuste de ${formatarMoeda(e.ajusteTotal)}`,
    })),
    ...apolice.sinistros.map<EventoTimeline>((s) => ({
      id: `sinistro-${s.id}`,
      quando: s.abertoEm,
      tom: "warn",
      titulo: (
        <>
          Sinistro{" "}
          <Link to={`/sinistros/${s.id}`} className="mono-id text-link hover:underline">
            {s.numero}
          </Link>{" "}
          aberto
        </>
      ),
      descricao: `${s.coberturaNome} · ${formatarMoeda(s.valorReclamado)} reclamados`,
      extra: <StatusBadge entidade="sinistro" status={s.status} />,
    })),
    ...apolice.notificacoes.map<EventoTimeline>((n) => ({
      id: `notificacao-${n.id}`,
      quando: n.criadaEm,
      tom: "steel",
      titulo: n.tipo === "BoasVindas" ? "E-mail de boas-vindas enviado" : "E-mail enviado",
      descricao: n.assunto,
    })),
  ];
  if (apolice.canceladaEm) {
    eventos.push({
      id: "cancelamento",
      quando: apolice.canceladaEm,
      tom: "bad",
      titulo: "Apólice cancelada",
      descricao: apolice.motivoCancelamento
        ? `${ROTULO_MOTIVO_CANCELAMENTO[apolice.motivoCancelamento]}${apolice.valorRestituicao === null ? "" : ` · restituição de ${formatarMoeda(apolice.valorRestituicao)}`}`
        : undefined,
    });
  }
  return eventos;
}

/** Protocolo derivado do número e da data de emissão; o registro é simulado (projeto de portfólio). */
function protocolo(apolice: ApoliceDto): string {
  const data = apolice.emitidaEm.slice(0, 10).replaceAll("-", "");
  const sequencia = apolice.numero.replace(/\D/g, "").slice(-7);
  return `${data.slice(0, 4)}.${data.slice(4)}.${sequencia}`;
}

/** Coluna lateral do detalhe (DESIGN.md §5.3): linha do tempo, partes e registro simulado. */
export function ColunaLateral({ apolice }: { apolice: ApoliceDto }) {
  const idade = idadeEm(apolice.segurado.dataNascimento, hojeEmBrasilia());
  return (
    <aside aria-label="Histórico e partes da apólice" className="flex flex-col gap-4 md:gap-6">
      <Card>
        <CardHeader titulo="Linha do tempo" subtitulo="Eventos registrados na apólice" />
        <CardBody className="pt-4">
          <Timeline
            eventos={eventosDa(apolice)}
            hoje={new Date()}
            rotulo="Linha do tempo da apólice"
          />
        </CardBody>
      </Card>
      <Card>
        <CardHeader titulo="Partes" />
        <CardBody className="pt-2">
          <ul className="divide-y divide-line-soft">
            <li className="flex items-center gap-3 py-3">
              <Avatar nome={apolice.segurado.nome} variante="steel" />
              <span className="min-w-0">
                <span className="block truncate text-sm font-semibold text-ink">
                  {apolice.segurado.nome}
                </span>
                <span className="block type-meta text-ink-3">Segurado · {idade} anos</span>
              </span>
            </li>
            <li className="flex items-center gap-3 py-3">
              <Avatar nome={apolice.corretorNome} />
              <span className="min-w-0">
                <span className="block truncate text-sm font-semibold text-ink">
                  {apolice.corretorNome}
                </span>
                <span className="block type-meta text-ink-3">Corretor responsável</span>
              </span>
            </li>
          </ul>
        </CardBody>
      </Card>
      <div className="flex items-center gap-3 rounded-xl border border-line bg-surface px-5 py-4">
        <span
          aria-hidden
          className="relative grid size-11 shrink-0 place-items-center overflow-hidden rounded-full bg-brand"
        >
          <Gravura tamanho="sm" className="absolute size-20" />
        </span>
        <p className="type-meta text-ink-3">
          Registro SRO · <strong className="font-semibold text-ink-2">simulado</strong>
          <span className="block">
            protocolo <span className="mono-id text-ink-2">{protocolo(apolice)}</span>
          </span>
        </p>
      </div>
    </aside>
  );
}

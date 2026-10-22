import { Calculator, LifeBuoy, ShieldCheck, type LucideIcon } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link } from "react-router";

import { useApolices } from "@/api/consultas/apolices";
import { useCotacoes } from "@/api/consultas/cotacoes";
import { useSinistros } from "@/api/consultas/sinistros";
import { Avatar } from "@/ui/componentes/Avatar";
import { StatusBadge } from "@/ui/componentes/Badge";
import { Card, CardHeader } from "@/ui/componentes/Card";
import { SegmentedControl } from "@/ui/componentes/SegmentedControl";
import { SkeletonTexto } from "@/ui/componentes/Skeleton";
import { DateText } from "@/ui/componentes/Valores";
import { cn } from "@/ui/utils/cn";
import { formatarMoeda } from "@/ui/utils/formatar";

import { referenciaCurta } from "../comum/rotulos";

type Tipo = "emissao" | "sinistro" | "cotacao";
type Filtro = "tudo" | Tipo;

interface Evento {
  id: string;
  tipo: Tipo;
  quando: string;
  identificador: string;
  href: string;
  titulo: string;
  detalhe: string;
  responsavel: string | null;
  status: ReactNode;
}

const ICONE: Record<Tipo, { icone: LucideIcon; classe: string }> = {
  emissao: { icone: ShieldCheck, classe: "bg-info-bg text-info" },
  sinistro: { icone: LifeBuoy, classe: "bg-warn-bg text-warn" },
  cotacao: { icone: Calculator, classe: "bg-neutral-bg text-neutral" },
};

const FILTROS = [
  { valor: "tudo", rotulo: "Tudo" },
  { valor: "emissao", rotulo: "Emissões" },
  { valor: "sinistro", rotulo: "Sinistros" },
  { valor: "cotacao", rotulo: "Cotações" },
] as const;

const LIMITE_EVENTOS = 6;

/**
 * Atividade recente montada das primeiras páginas das listas (a API não tem feed): emissões,
 * sinistros abertos e cotações calculadas, mais recente primeiro.
 */
function useEventos(): { eventos: Evento[]; carregando: boolean } {
  const apolices = useApolices({});
  const sinistros = useSinistros({});
  const cotacoes = useCotacoes({});
  const eventos: Evento[] = [
    ...(apolices.data?.pages[0]?.itens ?? []).map((a) => ({
      id: `a-${a.id}`,
      tipo: "emissao" as const,
      quando: a.emitidaEm,
      identificador: a.numero,
      href: `/apolices/${a.id}`,
      titulo: "Apólice emitida",
      detalhe: `${a.seguradoNome} · ${formatarMoeda(a.premioTotal)}`,
      responsavel: a.corretorNome,
      status: <StatusBadge entidade="apolice" status={a.status} />,
    })),
    ...(sinistros.data?.pages[0]?.itens ?? []).map((s) => ({
      id: `s-${s.id}`,
      tipo: "sinistro" as const,
      quando: s.abertoEm,
      identificador: s.numero,
      href: `/sinistros/${s.id}`,
      titulo: "Sinistro aberto",
      detalhe: `${s.seguradoNome} · ${s.coberturaNome} · ${s.apoliceNumero}`,
      responsavel: null,
      status: <StatusBadge entidade="sinistro" status={s.status} />,
    })),
    ...(cotacoes.data?.pages[0]?.itens ?? []).map((c) => ({
      id: `c-${c.id}`,
      tipo: "cotacao" as const,
      quando: c.criadaEm,
      identificador: `Ref. ${referenciaCurta(c.id)}`,
      href: `/cotacoes/${c.id}`,
      titulo: "Cotação calculada",
      detalhe: `${c.seguradoNome} · ${formatarMoeda(c.premioTotal)}`,
      responsavel: c.corretorNome,
      status: <StatusBadge entidade="cotacao" status={c.status} />,
    })),
  ].sort((a, b) => new Date(b.quando).getTime() - new Date(a.quando).getTime());
  return { eventos, carregando: apolices.isPending || sinistros.isPending || cotacoes.isPending };
}

function IconeEvento({ tipo }: { tipo: Tipo }) {
  const { icone: Icone, classe } = ICONE[tipo];
  return (
    <span aria-hidden className={cn("grid size-9 shrink-0 place-items-center rounded-lg", classe)}>
      <Icone size={17} />
    </span>
  );
}

function TabelaEventos({ eventos }: { eventos: Evento[] }) {
  return (
    <table className="hidden w-full table-fixed border-collapse md:table">
      <caption className="sr-only">Atividade recente</caption>
      <colgroup>
        <col className="w-[34%]" />
        <col />
        <col className="w-[22%]" />
        <col className="w-[15%]" />
      </colgroup>
      <thead>
        <tr>
          {["Evento", "Detalhe", "Responsável", "Quando"].map((c, i) => (
            <th
              key={c}
              scope="col"
              className={cn(
                "border-y border-line bg-surface-2 px-4 py-2.5 text-left text-meta font-semibold text-ink-2 first:pl-6 last:pr-6",
                i === 3 && "text-right",
              )}
            >
              {c}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {eventos.map((e) => (
          <tr key={e.id} className="hover:bg-surface-hover">
            <td className="border-b border-line-soft py-3 pr-4 pl-6 align-top">
              <span className="flex items-start gap-3">
                <IconeEvento tipo={e.tipo} />
                <span>
                  <span className="block text-sm font-semibold text-ink">{e.titulo}</span>
                  <Link to={e.href} className="mono-id whitespace-nowrap text-link hover:underline">
                    {e.identificador}
                  </Link>
                </span>
              </span>
            </td>
            <td className="border-b border-line-soft px-4 py-3 align-top text-sm text-ink-2">
              <span className="block">{e.detalhe}</span>
              <span className="mt-1 inline-block">{e.status}</span>
            </td>
            <td className="border-b border-line-soft px-4 py-3 align-top text-sm text-ink-2">
              {e.responsavel ? (
                <span className="inline-flex items-center gap-2">
                  <Avatar nome={e.responsavel} tamanho="sm" />
                  {e.responsavel}
                </span>
              ) : (
                "—"
              )}
            </td>
            <td className="border-b border-line-soft py-3 pr-6 pl-4 text-right align-top text-sm whitespace-nowrap text-ink-2">
              <DateText valor={e.quando} formato="relativo" />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ListaEventos({ eventos }: { eventos: Evento[] }) {
  return (
    <ul className="divide-y divide-line-soft border-t border-line md:hidden">
      {eventos.map((e) => (
        <li key={e.id}>
          <Link to={e.href} className="flex items-start gap-3 px-4 py-3.5 hover:bg-surface-hover">
            <IconeEvento tipo={e.tipo} />
            <span className="min-w-0 flex-1">
              <span className="block text-sm text-ink">
                {e.titulo}{" "}
                <span className="mono-id whitespace-nowrap text-link">{e.identificador}</span>
              </span>
              <span className="block truncate type-meta text-ink-3">{e.detalhe}</span>
            </span>
            <DateText
              valor={e.quando}
              formato="relativo"
              className="shrink-0 type-meta text-ink-3"
            />
          </Link>
        </li>
      ))}
    </ul>
  );
}

export function AtividadeRecente({ className }: { className?: string }) {
  const [filtro, setFiltro] = useState<Filtro>("tudo");
  const { eventos, carregando } = useEventos();
  const visiveis = eventos
    .filter((e) => filtro === "tudo" || e.tipo === filtro)
    .slice(0, LIMITE_EVENTOS);
  return (
    <Card className={className}>
      <CardHeader
        titulo="Atividade recente"
        subtitulo="Emissões, sinistros e cotações mais recentes"
        acoes={
          <SegmentedControl
            rotulo="Filtrar atividade"
            opcoes={FILTROS}
            valor={filtro}
            aoMudar={setFiltro}
            tamanho="sm"
            className="max-sm:hidden"
          />
        }
      />
      <div className="mt-4">
        {carregando ? (
          <div className="px-6 pb-6">
            <SkeletonTexto linhas={5} />
          </div>
        ) : visiveis.length === 0 ? (
          <p className="px-6 pb-6 text-sm text-ink-3">Nenhuma atividade registrada ainda.</p>
        ) : (
          <>
            <TabelaEventos eventos={visiveis} />
            <ListaEventos eventos={visiveis} />
          </>
        )}
      </div>
    </Card>
  );
}

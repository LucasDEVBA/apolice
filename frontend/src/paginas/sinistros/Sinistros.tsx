import { LifeBuoy, SearchX } from "lucide-react";
import { Link } from "react-router";

import { useSinistros } from "@/api/consultas/sinistros";
import type { SinistroResumoDto, StatusSinistro } from "@/api/contratos";
import { Pagina } from "@/app/shell/Pagina";
import { StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { DataTable, type ColunaTabela } from "@/ui/componentes/DataTable";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { Pagination } from "@/ui/componentes/Pagination";
import { DateText, Money } from "@/ui/componentes/Valores";

import { BarraDeFiltros, GrupoChips } from "../comum/BarraDeFiltros";
import { ErroDeLista } from "../comum/EstadoDaConsulta";
import { lerOpcao, useFiltrosDaUrl } from "../comum/filtros";
import { ScoreTriagem } from "./ScoreTriagem";

const STATUS: readonly StatusSinistro[] = ["Aberto", "EmAnalise", "Aprovado", "Negado", "Pago"];
const ROTULOS: Record<StatusSinistro, string> = {
  Aberto: "Aberto",
  EmAnalise: "Em análise",
  Aprovado: "Aprovado",
  Negado: "Negado",
  Pago: "Pago",
};

const COLUNAS: readonly ColunaTabela<SinistroResumoDto>[] = [
  {
    id: "numero",
    cabecalho: "Sinistro",
    codigo: true,
    celula: (s) => (
      <Link to={`/sinistros/${s.id}`} className="text-link hover:underline">
        {s.numero}
      </Link>
    ),
  },
  {
    id: "segurado",
    cabecalho: "Segurado",
    celula: (s) => (
      <>
        <span className="font-semibold">{s.seguradoNome}</span>
        <span className="block mono-id text-ink-3">{s.apoliceNumero}</span>
      </>
    ),
  },
  { id: "cobertura", cabecalho: "Cobertura", celula: (s) => s.coberturaNome },
  {
    id: "valor",
    cabecalho: "Reclamado",
    numerica: true,
    celula: (s) => <Money valor={s.valorReclamado} />,
  },
  {
    id: "triagem",
    cabecalho: "Triagem",
    numerica: true,
    celula: (s) => <ScoreTriagem score={s.triagemScore} compacto />,
  },
  {
    id: "aberto",
    cabecalho: "Aberto em",
    numerica: true,
    celula: (s) => <DateText valor={s.abertoEm} />,
  },
  {
    id: "status",
    cabecalho: "Situação",
    celula: (s) => <StatusBadge entidade="sinistro" status={s.status} />,
  },
];

function CartaoSinistro({ sinistro }: { sinistro: SinistroResumoDto }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-3">
        <span className="mono-id font-semibold text-link">{sinistro.numero}</span>
        <StatusBadge entidade="sinistro" status={sinistro.status} />
      </div>
      <p className="text-sm font-semibold text-ink">{sinistro.seguradoNome}</p>
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 type-meta text-ink-3">
        <span>{sinistro.coberturaNome}</span>
        <Money valor={sinistro.valorReclamado} className="font-semibold text-ink" />
        <DateText valor={sinistro.abertoEm} />
        <ScoreTriagem score={sinistro.triagemScore} compacto />
      </p>
    </div>
  );
}

export default function Sinistros() {
  const { params, definir, limpar } = useFiltrosDaUrl();
  const status = lerOpcao(params.get("status"), STATUS);
  const busca = params.get("busca") ?? "";
  const consulta = useSinistros({ status, busca: busca || null });
  const linhas = consulta.data?.pages.flatMap((p) => p.itens) ?? [];
  const comFiltro = Boolean(status ?? busca);
  return (
    <Pagina titulo="Sinistros" subtitulo="Avisos de sinistro com triagem automática e regulação">
      <Card>
        <BarraDeFiltros
          rotuloBusca="Buscar sinistro por número, apólice ou segurado"
          placeholderBusca="Sinistro, apólice ou segurado"
          busca={busca}
          aoBuscar={(termo) => termo !== busca && definir("busca", termo)}
        >
          <GrupoChips
            rotulo="Situação"
            opcoes={STATUS.map((s) => ({ valor: s, rotulo: ROTULOS[s] }))}
            ativo={status}
            aoMudar={(v) => definir("status", v)}
          />
        </BarraDeFiltros>
        {consulta.isError ? (
          <ErroDeLista
            erro={consulta.error}
            oQue="os sinistros"
            aoTentarDeNovo={() => void consulta.refetch()}
          />
        ) : (
          <>
            <DataTable
              legenda="Sinistros"
              colunas={COLUNAS}
              linhas={linhas}
              chave={(s) => s.id}
              hrefDaLinha={(s) => `/sinistros/${s.id}`}
              cartao={(s) => <CartaoSinistro sinistro={s} />}
              carregando={consulta.isPending}
              vazio={
                comFiltro ? (
                  <EmptyState
                    icone={SearchX}
                    titulo={
                      busca ? `Nada encontrado para “${busca}”` : "Nenhum sinistro com esse filtro"
                    }
                    descricao="Ajuste a busca ou limpe os filtros."
                    acao={
                      <Button variante="secundario" onClick={limpar}>
                        Limpar filtros
                      </Button>
                    }
                  />
                ) : (
                  <EmptyState
                    icone={LifeBuoy}
                    titulo="Nenhum sinistro aberto"
                    descricao="Os avisos de sinistro são abertos a partir da apólice."
                    acao={
                      <Button asChild>
                        <Link to="/apolices">Ver apólices</Link>
                      </Button>
                    }
                  />
                )
              }
            />
            {linhas.length > 0 && (
              <Pagination
                exibidos={linhas.length}
                temMais={consulta.hasNextPage}
                carregando={consulta.isFetchingNextPage}
                aoCarregarMais={() => void consulta.fetchNextPage()}
                substantivo="sinistros"
              />
            )}
          </>
        )}
      </Card>
    </Pagina>
  );
}

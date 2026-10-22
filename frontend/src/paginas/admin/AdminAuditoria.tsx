import { ScrollText } from "lucide-react";

import { useAuditoria } from "@/api/consultas/auditoria";
import type { AuditoriaDto } from "@/api/contratos";
import { Card } from "@/ui/componentes/Card";
import { DataTable, type ColunaTabela } from "@/ui/componentes/DataTable";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { Pagination } from "@/ui/componentes/Pagination";
import { DateText } from "@/ui/componentes/Valores";

import { ErroDeLista } from "../comum/EstadoDaConsulta";
import { rotuloAcaoAuditoria, rotuloEntidadeAuditoria } from "../comum/rotulos";
import { AreaAdmin } from "./AreaAdmin";

const LIMITE_DETALHES = 120;

function resumirDetalhes(detalhes: string | null): string {
  if (!detalhes) return "—";
  return detalhes.length > LIMITE_DETALHES ? `${detalhes.slice(0, LIMITE_DETALHES)}…` : detalhes;
}

const COLUNAS: readonly ColunaTabela<AuditoriaDto>[] = [
  {
    id: "quando",
    cabecalho: "Quando",
    celula: (a) => (
      <DateText valor={a.ocorridoEm} formato="data-hora" className="whitespace-nowrap" />
    ),
  },
  {
    id: "acao",
    cabecalho: "Ação",
    celula: (a) => (
      <>
        <span className="block font-semibold">{rotuloAcaoAuditoria(a.acao)}</span>
        <span className="block mono-id text-ink-3">{a.acao}</span>
      </>
    ),
  },
  {
    id: "usuario",
    cabecalho: "Usuário",
    celula: (a) => a.usuarioNome ?? <span className="text-ink-3">Sistema</span>,
  },
  {
    id: "entidade",
    cabecalho: "Entidade",
    celula: (a) => (
      <>
        {rotuloEntidadeAuditoria(a.entidade)}
        {a.entidadeId && (
          <span className="block max-w-[180px] truncate mono-id text-ink-3" title={a.entidadeId}>
            {a.entidadeId}
          </span>
        )}
      </>
    ),
  },
  {
    id: "detalhes",
    cabecalho: "Detalhes",
    celula: (a) => (
      <code
        className="block max-w-[320px] font-mono text-xs break-all text-ink-2"
        title={a.detalhes ?? undefined}
      >
        {resumirDetalhes(a.detalhes)}
      </code>
    ),
  },
  {
    id: "correlacao",
    cabecalho: "Correlação",
    celula: (a) =>
      a.correlationId ? (
        <span className="block max-w-[140px] truncate mono-id text-ink-3" title={a.correlationId}>
          {a.correlationId}
        </span>
      ) : (
        "—"
      ),
  },
];

export default function AdminAuditoria() {
  const consulta = useAuditoria();
  const linhas = consulta.data?.pages.flatMap((p) => p.itens) ?? [];
  return (
    <AreaAdmin
      titulo="Auditoria"
      subtitulo="Trilha de ações sensíveis: emissões, endossos, cancelamentos, sinistros e tarifas"
    >
      <Card className="pt-2">
        {consulta.isError ? (
          <ErroDeLista
            erro={consulta.error}
            oQue="a trilha de auditoria"
            aoTentarDeNovo={() => void consulta.refetch()}
          />
        ) : (
          <>
            <DataTable
              legenda="Trilha de auditoria"
              colunas={COLUNAS}
              linhas={linhas}
              chave={(a) => a.id}
              carregando={consulta.isPending}
              vazio={
                <EmptyState
                  icone={ScrollText}
                  titulo="Nenhum registro de auditoria"
                  descricao="As ações sensíveis aparecem aqui assim que acontecem."
                />
              }
            />
            {linhas.length > 0 && (
              <Pagination
                exibidos={linhas.length}
                temMais={consulta.hasNextPage}
                carregando={consulta.isFetchingNextPage}
                aoCarregarMais={() => void consulta.fetchNextPage()}
                substantivo="registros"
              />
            )}
          </>
        )}
      </Card>
    </AreaAdmin>
  );
}

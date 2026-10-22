import { SearchX, ShieldCheck, X } from "lucide-react";
import { Link } from "react-router";

import { useApolices } from "@/api/consultas/apolices";
import { LIMITE_MAXIMO_PAGINA, TAMANHO_PAGINA } from "@/api/consultas/chaves";
import type { ApoliceResumoDto, CodigoProduto, StatusEfetivoApolice } from "@/api/contratos";
import { Pagina } from "@/app/shell/Pagina";
import { Badge, StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { DataTable, type ColunaTabela } from "@/ui/componentes/DataTable";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { ChipFiltro } from "@/ui/componentes/FiltrosTabela";
import { Pagination } from "@/ui/componentes/Pagination";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { DateText, Money } from "@/ui/componentes/Valores";
import { diasEntre, hojeEmBrasilia } from "@/ui/utils/datas";
import { formatarNumero } from "@/ui/utils/formatar";

import { BarraDeFiltros, GrupoChips } from "../comum/BarraDeFiltros";
import { ErroDeLista } from "../comum/EstadoDaConsulta";
import { lerOpcao, useFiltrosDaUrl } from "../comum/filtros";
import { useTodasAsPaginas } from "../comum/useTodasAsPaginas";
import { venceEmAte30Dias } from "./renovacao";

const STATUS: readonly StatusEfetivoApolice[] = ["Ativa", "Programada", "Vencida", "Cancelada"];
const OPCOES_STATUS = STATUS.map((s) => ({ valor: s, rotulo: s }));
const OPCOES_PRODUTO: readonly { valor: CodigoProduto; rotulo: string }[] = [
  { valor: "AUTO", rotulo: "Auto" },
  { valor: "RESID", rotulo: "Residencial" },
  { valor: "VIDA", rotulo: "Vida" },
];
const FILA_RENOVACAO = "renovacao-30d";

function ChipVencimento({ apolice }: { apolice: ApoliceResumoDto }) {
  if (!venceEmAte30Dias(apolice)) return null;
  const dias = diasEntre(hojeEmBrasilia(), apolice.vigenciaFim);
  return (
    <Badge tom="neutral" marcador="plain">
      {dias === 0 ? "vence hoje" : `vence em ${String(dias)} ${dias === 1 ? "dia" : "dias"}`}
    </Badge>
  );
}

const COLUNAS: readonly ColunaTabela<ApoliceResumoDto>[] = [
  {
    id: "numero",
    cabecalho: "Apólice",
    codigo: true,
    celula: (a) => (
      <Link to={`/apolices/${a.id}`} className="text-link hover:underline">
        {a.numero}
      </Link>
    ),
  },
  {
    id: "segurado",
    cabecalho: "Segurado",
    celula: (a) => (
      <>
        <span className="font-semibold">{a.seguradoNome}</span>
        <span className="block type-meta text-ink-3 tnum">CPF {a.seguradoCpfMascarado}</span>
      </>
    ),
  },
  { id: "produto", cabecalho: "Produto", celula: (a) => <ProdutoTag produto={a.produtoCodigo} /> },
  {
    id: "vigencia",
    cabecalho: "Vigência",
    numerica: true,
    celula: (a) => (
      <span className="inline-flex flex-col items-end gap-1">
        <span>
          <DateText valor={a.vigenciaInicio} /> a <DateText valor={a.vigenciaFim} />
        </span>
        <ChipVencimento apolice={a} />
      </span>
    ),
  },
  {
    id: "premio",
    cabecalho: "Prêmio total",
    numerica: true,
    celula: (a) => <Money valor={a.premioTotal} />,
  },
  {
    id: "corretor",
    cabecalho: "Corretor",
    celula: (a) => <span className="text-ink-2">{a.corretorNome}</span>,
  },
  {
    id: "status",
    cabecalho: "Situação",
    celula: (a) => <StatusBadge entidade="apolice" status={a.status} />,
  },
];

function CartaoApolice({ apolice }: { apolice: ApoliceResumoDto }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-3">
        <span className="mono-id font-semibold text-link">{apolice.numero}</span>
        <StatusBadge entidade="apolice" status={apolice.status} />
      </div>
      <p className="text-sm font-semibold text-ink">{apolice.seguradoNome}</p>
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 type-meta text-ink-3">
        <ProdutoTag produto={apolice.produtoCodigo} />
        <Money valor={apolice.premioTotal} className="font-semibold text-ink" />
        <span>
          até <DateText valor={apolice.vigenciaFim} />
        </span>
        <ChipVencimento apolice={apolice} />
      </p>
    </div>
  );
}

export default function Apolices() {
  const { params, definir, limpar } = useFiltrosDaUrl();
  const status = lerOpcao(params.get("status"), STATUS);
  const produto = lerOpcao(
    params.get("produto"),
    OPCOES_PRODUTO.map((o) => o.valor),
  );
  const busca = params.get("busca") ?? "";
  const fila = params.get("fila") === FILA_RENOVACAO ? FILA_RENOVACAO : null;
  // A API não filtra por fim de vigência: a fila de renovação lê as ativas até o fim e filtra aqui.
  const consulta = useApolices(
    { status: fila ? "Ativa" : status, produto, busca: busca || null },
    fila ? LIMITE_MAXIMO_PAGINA : TAMANHO_PAGINA,
  );
  const progresso = useTodasAsPaginas(consulta, fila !== null);
  const todas = consulta.data?.pages.flatMap((p) => p.itens) ?? [];
  const linhas = fila ? todas.filter((a) => venceEmAte30Dias(a)) : todas;
  const comFiltro = Boolean(status ?? produto ?? busca) || fila !== null;

  return (
    <Pagina titulo="Apólices" subtitulo="Apólices emitidas, com vigência, prêmio e situação">
      <Card>
        <BarraDeFiltros
          rotuloBusca="Buscar apólice por número, segurado ou CPF"
          placeholderBusca="Número, segurado ou CPF"
          busca={busca}
          aoBuscar={(termo) => termo !== busca && definir("busca", termo)}
        >
          {!fila && (
            <GrupoChips
              rotulo="Situação"
              opcoes={OPCOES_STATUS}
              ativo={status}
              aoMudar={(v) => definir("status", v)}
            />
          )}
          <GrupoChips
            rotulo="Produto"
            opcoes={OPCOES_PRODUTO}
            ativo={produto}
            aoMudar={(v) => definir("produto", v)}
          />
          {fila && (
            <ChipFiltro
              ativo
              onClick={() => definir("fila", null)}
              aria-label="Remover filtro: renovações em 30 dias"
            >
              Renovações em 30 dias
              <X size={13} aria-hidden />
            </ChipFiltro>
          )}
        </BarraDeFiltros>
        {fila && (
          <p className="px-4 pb-3 type-meta text-ink-3 md:px-6">
            Apólices ativas cuja vigência termina nos próximos 30 dias
            {progresso.truncado
              ? `, entre as ${formatarNumero(todas.length)} mais recentes. Carregue mais para procurar nas anteriores.`
              : "."}
          </p>
        )}
        {consulta.isError ? (
          <ErroDeLista
            erro={consulta.error}
            oQue="as apólices"
            aoTentarDeNovo={() => void consulta.refetch()}
          />
        ) : (
          <>
            <DataTable
              legenda="Apólices"
              colunas={COLUNAS}
              linhas={linhas}
              chave={(a) => a.id}
              hrefDaLinha={(a) => `/apolices/${a.id}`}
              cartao={(a) => <CartaoApolice apolice={a} />}
              carregando={consulta.isPending || progresso.carregandoTudo}
              vazio={
                comFiltro ? (
                  <EmptyState
                    icone={SearchX}
                    titulo={
                      busca
                        ? `Nada encontrado para “${busca}”`
                        : "Nenhuma apólice com esses filtros"
                    }
                    descricao="Ajuste a busca ou limpe os filtros para ver todas as apólices."
                    acao={
                      <Button variante="secundario" onClick={limpar}>
                        Limpar filtros
                      </Button>
                    }
                  />
                ) : (
                  <EmptyState
                    icone={ShieldCheck}
                    titulo="Nenhuma apólice emitida ainda"
                    descricao="Uma apólice nasce de uma cotação aceita. Comece por uma nova cotação."
                    acao={
                      <Button asChild>
                        <Link to="/cotacoes">Ver cotações</Link>
                      </Button>
                    }
                  />
                )
              }
            />
            {(linhas.length > 0 || progresso.truncado) && (
              <Pagination
                exibidos={linhas.length}
                temMais={consulta.hasNextPage}
                carregando={consulta.isFetchingNextPage}
                aoCarregarMais={() => void consulta.fetchNextPage()}
                substantivo="apólices"
              />
            )}
          </>
        )}
      </Card>
    </Pagina>
  );
}

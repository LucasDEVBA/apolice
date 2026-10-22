import { Calculator, Plus, SearchX, X } from "lucide-react";
import { Link } from "react-router";

import type { CodigoProduto, CotacaoResumoDto, StatusEfetivoCotacao } from "@/api/contratos";
import { LIMITE_MAXIMO_PAGINA, TAMANHO_PAGINA } from "@/api/consultas/chaves";
import { useCotacoes } from "@/api/consultas/cotacoes";
import { pode } from "@/app/sessao/permissoes";
import { useUsuario } from "@/app/sessao/sessao-contexto";
import { Pagina } from "@/app/shell/Pagina";
import { StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { DataTable, type ColunaTabela } from "@/ui/componentes/DataTable";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { ChipFiltro } from "@/ui/componentes/FiltrosTabela";
import { Pagination } from "@/ui/componentes/Pagination";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { DateText, Money } from "@/ui/componentes/Valores";
import { formatarNumero } from "@/ui/utils/formatar";

import { ErroDeLista } from "../comum/EstadoDaConsulta";
import { BarraDeFiltros, GrupoChips } from "../comum/BarraDeFiltros";
import { lerOpcao, useFiltrosDaUrl } from "../comum/filtros";
import { referenciaCurta } from "../comum/rotulos";
import { useTodasAsPaginas } from "../comum/useTodasAsPaginas";
import { ChipPrazo } from "./ChipPrazo";
import { expiraEmBreve } from "./prazo";

const STATUS: readonly StatusEfetivoCotacao[] = [
  "Calculada",
  "Aceita",
  "Emitida",
  "Recusada",
  "Expirada",
];
const OPCOES_STATUS = STATUS.map((s) => ({ valor: s, rotulo: s }));
const OPCOES_PRODUTO: readonly { valor: CodigoProduto; rotulo: string }[] = [
  { valor: "AUTO", rotulo: "Auto" },
  { valor: "RESID", rotulo: "Residencial" },
  { valor: "VIDA", rotulo: "Vida" },
];
const FILA_EXPIRANDO = "expirando-48h";

const COLUNAS: readonly ColunaTabela<CotacaoResumoDto>[] = [
  {
    id: "segurado",
    cabecalho: "Segurado",
    celula: (c) => (
      <>
        <Link to={`/cotacoes/${c.id}`} className="font-semibold text-link hover:underline">
          {c.seguradoNome}
        </Link>
        <span className="block type-meta text-ink-3">
          Ref. <span className="mono-id">{referenciaCurta(c.id)}</span> · {c.corretorNome}
        </span>
      </>
    ),
  },
  { id: "produto", cabecalho: "Produto", celula: (c) => <ProdutoTag produto={c.produtoCodigo} /> },
  {
    id: "premio",
    cabecalho: "Prêmio total",
    numerica: true,
    celula: (c) => <Money valor={c.premioTotal} />,
  },
  {
    id: "validade",
    cabecalho: "Válida até",
    numerica: true,
    celula: (c) => (
      <span className="inline-flex flex-col items-end gap-1">
        <DateText valor={c.validaAte} />
        {expiraEmBreve(c) && <ChipPrazo validaAte={c.validaAte} />}
      </span>
    ),
  },
  {
    id: "criada",
    cabecalho: "Criada em",
    numerica: true,
    celula: (c) => <DateText valor={c.criadaEm} />,
  },
  {
    id: "status",
    cabecalho: "Situação",
    celula: (c) => <StatusBadge entidade="cotacao" status={c.status} />,
  },
];

function CartaoCotacao({ cotacao }: { cotacao: CotacaoResumoDto }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-3">
        <span className="mono-id text-ink-2">Ref. {referenciaCurta(cotacao.id)}</span>
        <StatusBadge entidade="cotacao" status={cotacao.status} />
      </div>
      <p className="text-sm font-semibold text-ink">{cotacao.seguradoNome}</p>
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 type-meta text-ink-3">
        <ProdutoTag produto={cotacao.produtoCodigo} />
        <Money valor={cotacao.premioTotal} className="font-semibold text-ink" />
        <span>
          até <DateText valor={cotacao.validaAte} />
        </span>
        {expiraEmBreve(cotacao) && <ChipPrazo validaAte={cotacao.validaAte} />}
      </p>
    </div>
  );
}

export default function Cotacoes() {
  const usuario = useUsuario();
  const { params, definir, limpar } = useFiltrosDaUrl();
  const status = lerOpcao(params.get("status"), STATUS);
  const produto = lerOpcao(
    params.get("produto"),
    OPCOES_PRODUTO.map((o) => o.valor),
  );
  const busca = params.get("busca") ?? "";
  const fila = params.get("fila") === FILA_EXPIRANDO ? FILA_EXPIRANDO : null;
  // A API não filtra por prazo de validade: a fila "expirando" lê as cotações até o fim e filtra
  // aqui (as que expiram são as antigas, criadas há cerca de 28 dias, no fim da paginação).
  const consulta = useCotacoes(
    { status, produto, busca: busca || null },
    fila ? LIMITE_MAXIMO_PAGINA : TAMANHO_PAGINA,
  );
  const progresso = useTodasAsPaginas(consulta, fila !== null);
  const todas = consulta.data?.pages.flatMap((p) => p.itens) ?? [];
  const linhas = fila ? todas.filter(expiraEmBreve) : todas;
  const comFiltro = Boolean(status ?? produto ?? busca) || fila !== null;
  const podeCotar = pode(usuario.papel, "operarCarteira");

  const vazio = comFiltro ? (
    <EmptyState
      icone={SearchX}
      titulo={busca ? `Nada encontrado para “${busca}”` : "Nenhuma cotação com esses filtros"}
      descricao="Ajuste a busca ou limpe os filtros para ver todas as cotações."
      acao={
        <Button variante="secundario" onClick={limpar}>
          Limpar filtros
        </Button>
      }
    />
  ) : (
    <EmptyState
      icone={Calculator}
      titulo="Nenhuma cotação ainda"
      descricao="As cotações calculadas aparecem aqui, com validade de 30 dias."
      {...(podeCotar
        ? {
            acao: (
              <Button asChild icone={Plus}>
                <Link to="/cotacoes/nova">Nova cotação</Link>
              </Button>
            ),
          }
        : {})}
    />
  );

  return (
    <Pagina
      titulo="Cotações"
      subtitulo="Cálculos de prêmio da carteira, válidos por 30 dias"
      acoes={
        podeCotar && (
          <Button asChild icone={Plus} className="max-md:h-[46px] max-md:w-full">
            <Link to="/cotacoes/nova">Nova cotação</Link>
          </Button>
        )
      }
    >
      <Card>
        <BarraDeFiltros
          rotuloBusca="Buscar cotação por segurado ou CPF"
          placeholderBusca="Segurado ou CPF"
          busca={busca}
          aoBuscar={(termo) => termo !== busca && definir("busca", termo)}
        >
          <GrupoChips
            rotulo="Situação"
            opcoes={OPCOES_STATUS}
            ativo={status}
            aoMudar={(v) => definir("status", v)}
          />
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
              aria-label="Remover filtro: expirando em 48 horas"
            >
              Expirando em 48 h
              <X size={13} aria-hidden />
            </ChipFiltro>
          )}
        </BarraDeFiltros>
        {fila && (
          <p className="px-4 pb-3 type-meta text-ink-3 md:px-6">
            Cotações calculadas ou aceitas que expiram nas próximas 48 horas
            {progresso.truncado
              ? `, entre as ${formatarNumero(todas.length)} mais recentes. Carregue mais para procurar nas anteriores.`
              : "."}
          </p>
        )}
        {consulta.isError ? (
          <ErroDeLista
            erro={consulta.error}
            oQue="as cotações"
            aoTentarDeNovo={() => void consulta.refetch()}
          />
        ) : (
          <>
            <DataTable
              legenda="Cotações"
              colunas={COLUNAS}
              linhas={linhas}
              chave={(c) => c.id}
              hrefDaLinha={(c) => `/cotacoes/${c.id}`}
              cartao={(c) => <CartaoCotacao cotacao={c} />}
              carregando={consulta.isPending || progresso.carregandoTudo}
              vazio={vazio}
            />
            {(linhas.length > 0 || progresso.truncado) && (
              <Pagination
                exibidos={linhas.length}
                temMais={consulta.hasNextPage}
                carregando={consulta.isFetchingNextPage}
                aoCarregarMais={() => void consulta.fetchNextPage()}
                substantivo="cotações"
              />
            )}
          </>
        )}
      </Card>
    </Pagina>
  );
}

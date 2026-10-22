import { Ban, Check, MoveDownRight, MoveUpRight, RotateCw, ShieldCheck } from "lucide-react";
import type { ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router";

import { useAceitarCotacao, useCotacao, useRecusarCotacao } from "@/api/consultas/cotacoes";
import { useProduto } from "@/api/consultas/produtos";
import type { CotacaoDto, DadosRisco } from "@/api/contratos";
import { pode } from "@/app/sessao/permissoes";
import { useUsuario } from "@/app/sessao/sessao-contexto";
import { CarregandoPagina } from "@/app/shell/CarregandoPagina";
import { Pagina } from "@/app/shell/Pagina";
import { StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { ConfirmDialog } from "@/ui/componentes/Dialog";
import { notificar } from "@/ui/componentes/notificar";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { DateText, Money } from "@/ui/componentes/Valores";
import { cn } from "@/ui/utils/cn";
import { formatarCep } from "@/ui/utils/documentos";
import { formatarFator, formatarMoeda, formatarPercentual, mascararCpf } from "@/ui/utils/formatar";

import { EstadoDaConsulta } from "../comum/EstadoDaConsulta";
import { ListaCoberturasCompacta } from "../comum/ListaCoberturasCompacta";
import { referenciaCurta } from "../comum/rotulos";
import { ChipPrazo } from "./ChipPrazo";
import { DialogoEmitir } from "./DialogoEmitir";
import { expiraEmBreve } from "./prazo";
import { prepararRecalculo } from "./recalcular";

function Dado({ rotulo, children }: { rotulo: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 py-2">
      <dt className="text-label text-ink-3">{rotulo}</dt>
      <dd className="text-right text-sm text-ink tnum">{children}</dd>
    </div>
  );
}

function DadosDoRisco({ risco }: { risco: DadosRisco }) {
  switch (risco.produto) {
    case "AUTO":
      return (
        <>
          <Dado rotulo="Valor do veículo">{formatarMoeda(risco.valorVeiculo)}</Dado>
          <Dado rotulo="Ano de fabricação">{risco.anoFabricacao}</Dado>
          <Dado rotulo="CEP de pernoite">{formatarCep(risco.cepPernoite)}</Dado>
          <Dado rotulo="Condutor principal">{risco.idadeCondutor} anos</Dado>
          <Dado rotulo="Uso">{risco.usoComercial ? "Comercial" : "Particular"}</Dado>
          <Dado rotulo="Classe de bônus">{risco.classeBonus}</Dado>
        </>
      );
    case "RESID":
      return (
        <>
          <Dado rotulo="Valor do imóvel">{formatarMoeda(risco.valorImovel)}</Dado>
          <Dado rotulo="Tipo">{risco.tipoImovel}</Dado>
          <Dado rotulo="CEP">{formatarCep(risco.cep)}</Dado>
          <Dado rotulo="Ocupação">{risco.habitual ? "Habitual" : "Veraneio"}</Dado>
        </>
      );
    case "VIDA":
      return <Dado rotulo="Hábito de fumar">{risco.fumante ? "Fumante" : "Não fumante"}</Dado>;
  }
}

function TabelaItens({ cotacao }: { cotacao: CotacaoDto }) {
  return (
    <div className="relative overflow-x-auto">
      <table className="w-full min-w-[520px] border-collapse text-(length:--fs-table)">
        <caption className="sr-only">Coberturas cotadas</caption>
        <thead>
          <tr className="text-meta text-ink-2">
            {["Código", "Cobertura", "LMI", "Prêmio"].map((c, i) => (
              <th
                key={c}
                scope="col"
                className={cn(
                  "border-y border-line bg-surface-2 px-4 py-2.5 font-semibold first:pl-6 last:pr-6",
                  i >= 2 ? "text-right" : "text-left",
                )}
              >
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {cotacao.itens.map((item) => (
            <tr key={item.codigo}>
              <td className="border-b border-line-soft py-3 pr-4 pl-6 align-top mono-id font-semibold text-ink-2">
                {item.codigo}
              </td>
              <td className="border-b border-line-soft px-4 py-3 align-top text-ink">
                {item.nome}
                {item.obrigatoria && (
                  <span className="block type-meta text-ink-3">Obrigatória</span>
                )}
              </td>
              <td className="border-b border-line-soft px-4 py-3 text-right align-top text-ink tnum">
                {item.lmi > 0 ? formatarMoeda(item.lmi) : "—"}
              </td>
              <td className="border-b border-line-soft py-3 pr-6 pl-4 text-right align-top text-ink tnum">
                {formatarMoeda(item.premioLiquido)}
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th
              scope="row"
              colSpan={3}
              className="px-4 pt-4 pb-1 pl-6 text-left font-normal text-ink-2"
            >
              Prêmio líquido
            </th>
            <td className="pt-4 pr-6 pb-1 pl-4 text-right text-ink tnum">
              {formatarMoeda(cotacao.premioLiquido)}
            </td>
          </tr>
          <tr>
            <th scope="row" colSpan={3} className="px-4 py-1 pl-6 text-left font-normal text-ink-2">
              IOF ({formatarPercentual(cotacao.aliquotaIof)})
            </th>
            <td className="py-1 pr-6 pl-4 text-right text-ink tnum">
              {formatarMoeda(cotacao.iof)}
            </td>
          </tr>
          <tr>
            <th
              scope="row"
              colSpan={3}
              className="px-4 pt-2 pb-5 pl-6 text-left font-semibold text-ink"
            >
              Prêmio total
            </th>
            <td className="pt-2 pr-6 pb-5 pl-4 text-right">
              <Money
                valor={cotacao.premioTotal}
                variante="heroi"
                className="type-fig-lg text-ink"
              />
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

function Fatores({ cotacao }: { cotacao: CotacaoDto }) {
  if (cotacao.fatores.length === 0) {
    return (
      <p className="text-sm text-ink-3">Nenhum fator alterou o preço: todos neutros (×1,00).</p>
    );
  }
  return (
    <ul className="divide-y divide-line-soft">
      {cotacao.fatores.map((f) => {
        const agrava = f.fator > 1;
        const Seta = agrava ? MoveUpRight : MoveDownRight;
        return (
          <li key={f.descricao} className="flex items-center justify-between gap-3 py-2.5">
            <span className="text-sm text-ink">{f.descricao}</span>
            <span
              className={cn(
                "inline-flex h-6 items-center gap-1 rounded-full px-2 text-label font-semibold tnum",
                agrava ? "bg-warn-bg text-warn" : "bg-ok-bg text-ok",
              )}
            >
              <Seta size={13} strokeWidth={2} aria-hidden />
              <span className="sr-only">{agrava ? "agrava" : "desconta"} </span>
              {formatarFator(f.fator)}
            </span>
          </li>
        );
      })}
    </ul>
  );
}

function AcoesDaCotacao({ cotacao }: { cotacao: CotacaoDto }) {
  const usuario = useUsuario();
  const navegar = useNavigate();
  const aceitar = useAceitarCotacao();
  const recusar = useRecusarCotacao();
  const produto = useProduto(cotacao.produtoCodigo);
  if (cotacao.status === "Emitida" && cotacao.apoliceId) {
    return (
      <Button asChild icone={ShieldCheck} variante="secundario">
        <Link to={`/apolices/${cotacao.apoliceId}`}>Ver apólice</Link>
      </Button>
    );
  }
  if (!pode(usuario.papel, "operarCarteira")) return null;
  if (cotacao.status === "Expirada") {
    return produto ? (
      <Button
        icone={RotateCw}
        onClick={() => {
          prepararRecalculo(cotacao, produto, usuario.id);
          void navegar("/cotacoes/nova?etapa=4");
        }}
      >
        Recalcular
      </Button>
    ) : null;
  }
  const podeRecusar = cotacao.status === "Calculada" || cotacao.status === "Aceita";
  // No mobile as ações empilham com a principal em cima (DESIGN.md §4.17): a ordem do DOM segue a
  // leitura (secundária → principal) e o flex-col-reverse inverte só a pilha visual.
  return (
    <div className="flex w-full flex-col-reverse gap-2.5 md:w-auto md:flex-row md:items-center [&>*]:w-full md:[&>*]:w-auto">
      {podeRecusar && (
        <ConfirmDialog
          gatilho={
            <Button variante="perigo-contorno" icone={Ban}>
              Recusar
            </Button>
          }
          titulo="Recusar cotação?"
          descricao={`A cotação de ${cotacao.segurado.nome} deixa de poder ser aceita ou emitida. Essa ação não pode ser desfeita.`}
          rotuloConfirmar="Recusar cotação"
          rotuloConfirmando="Recusando…"
          aoConfirmar={async () => {
            await recusar.mutateAsync(cotacao.id);
            notificar.info({ titulo: "Cotação recusada" });
          }}
        />
      )}
      {cotacao.status === "Calculada" && (
        <ConfirmDialog
          gatilho={<Button icone={Check}>Aceitar cotação</Button>}
          titulo="Aceitar cotação?"
          descricao={`O segurado aceitou o prêmio de ${formatarMoeda(cotacao.premioTotal)}. Depois de aceita, a cotação pode ser emitida até o fim da validade.`}
          rotuloConfirmar="Aceitar cotação"
          rotuloConfirmando="Aceitando…"
          tom="primario"
          aoConfirmar={async () => {
            await aceitar.mutateAsync(cotacao.id);
            notificar.sucesso({
              titulo: "Cotação aceita",
              descricao: "Agora ela pode ser emitida como apólice.",
            });
          }}
        />
      )}
      {cotacao.status === "Aceita" && <DialogoEmitir cotacao={cotacao} />}
    </div>
  );
}

function Conteudo({ cotacao }: { cotacao: CotacaoDto }) {
  const ref = referenciaCurta(cotacao.id);
  return (
    <Pagina
      titulo={`Cotação de ${cotacao.segurado.nome}`}
      subtitulo={
        <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
          <ProdutoTag produto={cotacao.produtoCodigo} />
          <span>
            criada em <DateText valor={cotacao.criadaEm} formato="data-hora" /> por{" "}
            {cotacao.corretorNome}
          </span>
        </span>
      }
      migalhas={[
        { rotulo: "Cotações", href: "/cotacoes" },
        { rotulo: `Ref. ${ref}`, codigo: true },
      ]}
      acoes={<AcoesDaCotacao cotacao={cotacao} />}
    >
      <div className="grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-[minmax(0,1fr)_380px] lg:items-start">
        <div className="flex flex-col gap-4 md:gap-6">
          <Card>
            <CardHeader
              titulo="Coberturas cotadas"
              subtitulo={`${String(cotacao.itens.length)} coberturas · prêmio anual por item`}
            />
            <div className="mt-4">
              <div className="hidden sm:block">
                <TabelaItens cotacao={cotacao} />
              </div>
              <ListaCoberturasCompacta
                rotulo="Coberturas cotadas"
                linhas={cotacao.itens.map((i) => ({
                  codigo: i.codigo,
                  nome: i.nome,
                  lmi: i.lmi,
                  premio: i.premioLiquido,
                }))}
                totais={[
                  { rotulo: "Prêmio líquido", valor: formatarMoeda(cotacao.premioLiquido) },
                  {
                    rotulo: `IOF (${formatarPercentual(cotacao.aliquotaIof)})`,
                    valor: formatarMoeda(cotacao.iof),
                  },
                  {
                    rotulo: "Prêmio total",
                    valor: formatarMoeda(cotacao.premioTotal),
                    forte: true,
                  },
                ]}
              />
            </div>
          </Card>
          <Card>
            <CardHeader
              titulo="Fatores aplicados"
              subtitulo="Multiplicam só os itens calculados por taxa; prêmio fixo não sofre fator."
            />
            <CardBody className="pt-3">
              <Fatores cotacao={cotacao} />
            </CardBody>
          </Card>
        </div>
        <aside aria-label="Resumo da cotação" className="flex flex-col gap-4 md:gap-6">
          <Card>
            <CardHeader titulo="Situação" />
            <CardBody className="pt-2">
              <dl className="divide-y divide-line-soft">
                <Dado rotulo="Status">
                  <StatusBadge entidade="cotacao" status={cotacao.status} />
                </Dado>
                <Dado rotulo="Válida até">
                  <span className="inline-flex flex-col items-end gap-1">
                    <DateText valor={cotacao.validaAte} formato="data-hora" />
                    {expiraEmBreve(cotacao) && <ChipPrazo validaAte={cotacao.validaAte} />}
                  </span>
                </Dado>
                <Dado rotulo="Parcelamento">até {cotacao.maxParcelas}×</Dado>
                <Dado rotulo="Referência">
                  <span className="mono-id">{ref}</span>
                </Dado>
              </dl>
            </CardBody>
          </Card>
          <Card>
            <CardHeader titulo="Segurado" />
            <CardBody className="pt-2">
              <dl className="divide-y divide-line-soft">
                <Dado rotulo="Nome">{cotacao.segurado.nome}</Dado>
                <Dado rotulo="CPF">{mascararCpf(cotacao.segurado.cpf)}</Dado>
                <Dado rotulo="E-mail">
                  <span className="break-all">{cotacao.segurado.email}</span>
                </Dado>
                <Dado rotulo="Nascimento">
                  <DateText valor={cotacao.segurado.dataNascimento} />
                </Dado>
              </dl>
            </CardBody>
          </Card>
          <Card>
            <CardHeader titulo="Risco" />
            <CardBody className="pt-2">
              <dl className="divide-y divide-line-soft">
                <DadosDoRisco risco={cotacao.dadosRisco} />
              </dl>
            </CardBody>
          </Card>
        </aside>
      </div>
    </Pagina>
  );
}

export default function CotacaoDetalhe() {
  const { id = "" } = useParams();
  const consulta = useCotacao(id);
  if (consulta.isPending) return <CarregandoPagina />;
  if (consulta.isError) {
    return (
      <Pagina
        titulo="Cotação"
        migalhas={[{ rotulo: "Cotações", href: "/cotacoes" }, { rotulo: "Cotação" }]}
      >
        <EstadoDaConsulta
          erro={consulta.error}
          entidade="a cotação"
          voltarPara={{ rotulo: "Voltar às cotações", href: "/cotacoes" }}
          aoTentarDeNovo={() => void consulta.refetch()}
        />
      </Pagina>
    );
  }
  return <Conteudo cotacao={consulta.data} />;
}

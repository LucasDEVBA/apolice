import { Ban, Download, FilePen, LifeBuoy, MoreHorizontal, TriangleAlert } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import type { ApoliceDto, DadosRisco } from "@/api/contratos";
import { Badge, StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/ui/componentes/DropdownMenu";
import { PRODUTOS } from "@/ui/componentes/produtos";
import { Money } from "@/ui/componentes/Valores";
import { Gravura } from "@/ui/marca/Marca";
import { diasEntre, hojeEmBrasilia, idadeEm } from "@/ui/utils/datas";
import { formatarCep } from "@/ui/utils/documentos";
import { formatarData, formatarMoeda, formatarNumero, mascararCpf } from "@/ui/utils/formatar";
import { calcularParcelas } from "@/ui/utils/parcelas";

import { ROTULO_MOTIVO_CANCELAMENTO } from "../comum/rotulos";

export interface AcoesDaFace {
  baixarPdf?: { executar: () => void; carregando: boolean };
  endossar?: () => void;
  cancelar?: () => void;
  abrirSinistro?: () => void;
}

function linhaDoRisco(risco: DadosRisco, nascimento: string): ReactNode {
  switch (risco.produto) {
    case "AUTO":
      return (
        <>
          Veículo{" "}
          <strong className="font-semibold text-on-navy">
            {formatarMoeda(risco.valorVeiculo)}
          </strong>{" "}
          · ano {risco.anoFabricacao} · CEP {formatarCep(risco.cepPernoite)} ·{" "}
          {risco.usoComercial ? "uso comercial" : "uso particular"}
        </>
      );
    case "RESID":
      return (
        <>
          {risco.tipoImovel} de{" "}
          <strong className="font-semibold text-on-navy">{formatarMoeda(risco.valorImovel)}</strong>{" "}
          · CEP {formatarCep(risco.cep)} · {risco.habitual ? "habitual" : "veraneio"}
        </>
      );
    case "VIDA":
      return (
        <>
          {idadeEm(nascimento, hojeEmBrasilia())} anos · {risco.fumante ? "fumante" : "não fumante"}
        </>
      );
  }
}

function sinistrosEmCurso(apolice: ApoliceDto) {
  return apolice.sinistros.filter((s) => s.status === "Aberto" || s.status === "EmAnalise");
}

/** Badge contextual (DESIGN.md §4.6): "1 sinistro em análise", que leva ao sinistro. */
function BadgeContextual({ apolice }: { apolice: ApoliceDto }) {
  const emCurso = sinistrosEmCurso(apolice);
  const primeiro = emCurso[0];
  if (!primeiro) return null;
  const texto =
    emCurso.length === 1 ? "1 sinistro em curso" : `${String(emCurso.length)} sinistros em curso`;
  return (
    <Link to={`/sinistros/${primeiro.id}`} className="rounded-full focus-visible:outline-gold-line">
      <Badge tom="warn" marcador={TriangleAlert} sobreMarinho>
        {texto}
      </Badge>
    </Link>
  );
}

function textoDaVigencia(apolice: ApoliceDto, hoje: string): { progresso: number; texto: string } {
  const total = diasEntre(apolice.vigenciaInicio, apolice.vigenciaFim);
  if (apolice.status === "Programada") {
    return {
      progresso: 0,
      texto: `começa em ${String(diasEntre(hoje, apolice.vigenciaInicio))} dias`,
    };
  }
  if (apolice.status === "Cancelada" && apolice.canceladaEm) {
    return { progresso: 0, texto: `cancelada em ${formatarData(apolice.canceladaEm)}` };
  }
  const decorridos = Math.min(total, Math.max(0, diasEntre(apolice.vigenciaInicio, hoje)));
  if (apolice.status === "Vencida")
    return { progresso: 1, texto: `encerrada em ${formatarData(apolice.vigenciaFim)}` };
  const restantes = total - decorridos;
  return {
    progresso: decorridos / total,
    texto: `${formatarNumero(decorridos)} de ${formatarNumero(total)} dias · renova em ${formatarNumero(restantes)} dias`,
  };
}

function Fato({ rotulo, children }: { rotulo: string; children: ReactNode }) {
  return (
    <div className="min-w-0 px-5 py-4 md:px-6 md:py-5">
      <dt className="text-label text-on-navy-3">{rotulo}</dt>
      <dd className="mt-1.5 text-on-navy">{children}</dd>
    </div>
  );
}

function FaixaDeFatos({ apolice }: { apolice: ApoliceDto }) {
  const hoje = hojeEmBrasilia();
  const vigencia = textoDaVigencia(apolice, hoje);
  const plano = calcularParcelas(apolice.premioTotal, apolice.parcelas);
  const principal = apolice.coberturas.find((c) => c.obrigatoria) ?? apolice.coberturas[0];
  return (
    <dl className="grid border-t border-gold-line-soft bg-brand-deep sm:grid-cols-2 xl:grid-cols-4 [&>div]:border-on-navy/8 max-sm:[&>div+div]:border-t xl:[&>div+div]:border-l sm:[&>div:nth-child(even)]:border-l sm:max-xl:[&>div:nth-child(n+3)]:border-t">
      <Fato rotulo="Vigência">
        <span className="block text-campo font-semibold tnum">
          {formatarData(apolice.vigenciaInicio)} a {formatarData(apolice.vigenciaFim)}
        </span>
        <span aria-hidden className="mt-2.5 block h-1 rounded-full bg-on-navy/12">
          <span
            className="block h-full rounded-full bg-gold-line"
            style={{ width: `${String(vigencia.progresso * 100)}%` }}
          />
        </span>
        <span className="mt-2 block type-meta text-on-navy-3 tnum">{vigencia.texto}</span>
      </Fato>
      <Fato rotulo="Prêmio total">
        <Money
          valor={apolice.premioTotal}
          variante="heroi"
          sobreMarinho
          className="block text-[1.875rem] leading-none"
        />
        <span className="mt-2 block type-meta text-on-navy-3 tnum">
          Líquido {formatarMoeda(apolice.premioLiquidoVigente)} + IOF
        </span>
      </Fato>
      <Fato rotulo="Parcelamento">
        <span className="block text-campo font-semibold tnum">
          {apolice.parcelas === 1
            ? `À vista`
            : `${String(apolice.parcelas)}× de ${formatarMoeda(plano.demais)}`}
        </span>
        <span className="mt-2 block type-meta text-on-navy-3 tnum">
          {apolice.parcelas === 1
            ? formatarMoeda(plano.primeira)
            : `1ª parcela de ${formatarMoeda(plano.primeira)}`}
        </span>
      </Fato>
      <Fato rotulo="LMI principal">
        <span className="block text-campo font-semibold tnum">
          {principal ? formatarMoeda(principal.lmi) : "—"}
        </span>
        <span className="mt-2 block truncate type-meta text-on-navy-3">{principal?.nome}</span>
      </Fato>
    </dl>
  );
}

function AcoesDesktop({ acoes }: { acoes: AcoesDaFace }) {
  return (
    <div className="hidden flex-wrap items-center gap-2 md:flex lg:justify-end xl:shrink-0 xl:flex-nowrap">
      {acoes.baixarPdf && (
        <Button
          variante="marinho-contorno"
          icone={Download}
          carregando={acoes.baixarPdf.carregando}
          rotuloCarregando="Baixando…"
          onClick={acoes.baixarPdf.executar}
        >
          Baixar PDF
        </Button>
      )}
      {acoes.abrirSinistro && (
        <Button variante="marinho-contorno" icone={LifeBuoy} onClick={acoes.abrirSinistro}>
          Abrir sinistro
        </Button>
      )}
      {acoes.endossar && (
        <Button variante="marinho-claro" icone={FilePen} onClick={acoes.endossar}>
          Endossar
        </Button>
      )}
      {acoes.cancelar && (
        <Button variante="marinho-perigo" icone={Ban} onClick={acoes.cancelar}>
          Cancelar
        </Button>
      )}
    </div>
  );
}

/** Mobile: "Endossar" + menu ⋯ com o resto (DESIGN.md §4.13). */
function AcoesMobile({ acoes }: { acoes: AcoesDaFace }) {
  const temMenu = Boolean(acoes.baixarPdf ?? acoes.abrirSinistro ?? acoes.cancelar);
  if (!acoes.endossar && !temMenu) return null;
  return (
    <div className="flex items-center gap-2 md:hidden">
      {acoes.endossar && (
        <Button
          variante="marinho-claro"
          icone={FilePen}
          onClick={acoes.endossar}
          className="flex-1"
        >
          Endossar
        </Button>
      )}
      {temMenu && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variante="marinho-contorno"
              icone={MoreHorizontal}
              aria-label="Mais ações da apólice"
              className={acoes.endossar ? "" : "flex-1"}
            >
              {acoes.endossar ? "" : "Ações"}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {acoes.baixarPdf && (
              <DropdownMenuItem icone={Download} onSelect={acoes.baixarPdf.executar}>
                Baixar PDF
              </DropdownMenuItem>
            )}
            {acoes.abrirSinistro && (
              <DropdownMenuItem icone={LifeBuoy} onSelect={acoes.abrirSinistro}>
                Abrir sinistro
              </DropdownMenuItem>
            )}
            {acoes.cancelar && (
              <DropdownMenuItem icone={Ban} perigo onSelect={acoes.cancelar}>
                Cancelar apólice
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  );
}

/**
 * Face da apólice como certificado (DESIGN.md §4.13): marinho com folha dourada e gravura, número
 * em serifa, status + badge contextual, segurado, ações e a faixa de fatos.
 */
export function FaceApolice({ apolice, acoes }: { apolice: ApoliceDto; acoes: AcoesDaFace }) {
  const { icone: Icone } = PRODUTOS[apolice.produtoCodigo];
  return (
    <section
      aria-labelledby="numero-apolice"
      className="on-navy relative overflow-hidden rounded-2xl border border-brand-line bg-brand text-on-navy"
    >
      <div aria-hidden className="h-1.5 folha-dourada" />
      <Gravura
        tamanho="lg"
        className="pointer-events-none absolute -top-[180px] -right-[160px] size-[520px] max-md:opacity-60"
      />
      <div className="relative flex flex-col gap-5 px-5 pt-5 pb-6 md:px-8 md:pt-7 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-label">
            <span className="inline-flex items-center gap-1.5 font-semibold text-gold-on-navy">
              <Icone size={15} aria-hidden />
              {apolice.produtoNome}
            </span>
            <span className="text-on-navy-3">
              Produto <span className="mono-id">{apolice.produtoCodigo}</span> · emitida em{" "}
              {formatarData(apolice.emitidaEm)}
            </span>
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-2">
            <h1
              id="numero-apolice"
              tabIndex={-1}
              className="type-display-lg tnum outline-none max-md:text-[1.75rem]"
            >
              {apolice.numero}
            </h1>
            <span className="flex flex-wrap items-center gap-2">
              <StatusBadge entidade="apolice" status={apolice.status} sobreMarinho />
              <BadgeContextual apolice={apolice} />
            </span>
          </div>
          <p className="mt-3 text-sm text-on-navy-2">
            Segurado <strong className="font-semibold text-on-navy">{apolice.segurado.nome}</strong>
            <span className="mx-2 text-on-navy-3" aria-hidden>
              ·
            </span>
            <span className="whitespace-nowrap">
              CPF{" "}
              <span className="font-semibold text-on-navy tnum">
                {mascararCpf(apolice.segurado.cpf)}
              </span>
            </span>
          </p>
          <p className="mt-1 text-sm text-on-navy-2 tnum">
            {linhaDoRisco(apolice.dadosRisco, apolice.segurado.dataNascimento)}
          </p>
          {apolice.status === "Cancelada" && apolice.motivoCancelamento && (
            <p className="mt-3 inline-flex flex-wrap items-center gap-x-2 rounded-md bg-bad-on-navy/12 px-3 py-1.5 text-label text-bad-on-navy">
              Cancelada por {ROTULO_MOTIVO_CANCELAMENTO[apolice.motivoCancelamento].toLowerCase()}
              {apolice.valorRestituicao !== null && (
                <> · restituição de {formatarMoeda(apolice.valorRestituicao)}</>
              )}
            </p>
          )}
        </div>
        <AcoesDesktop acoes={acoes} />
        <AcoesMobile acoes={acoes} />
      </div>
      <FaixaDeFatos apolice={apolice} />
    </section>
  );
}

import { Clock, LifeBuoy, LoaderCircle, Mail } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import type { ApoliceDto, ParcelaDto } from "@/api/contratos";
import { Badge, StatusBadge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { Tabs } from "@/ui/componentes/Tabs";
import { DateText, Money } from "@/ui/componentes/Valores";
import { cn } from "@/ui/utils/cn";
import { hojeEmBrasilia } from "@/ui/utils/datas";
import { formatarMoeda, formatarPercentual } from "@/ui/utils/formatar";

import { ListaCoberturasCompacta } from "../comum/ListaCoberturasCompacta";
import { ROTULO_TIPO_ENDOSSO, ROTULO_TIPO_NOTIFICACAO } from "../comum/rotulos";

const CELULA = "border-b border-line-soft px-4 py-(--row-y) align-top first:pl-6 last:pr-6";
const CABECALHO =
  "border-y border-line bg-surface-2 px-4 py-2.5 text-meta font-semibold text-ink-2 first:pl-6 last:pr-6";

function Tabela({
  legenda,
  cabecalhos,
  direita,
  children,
  rodape,
}: {
  legenda: string;
  cabecalhos: readonly string[];
  direita: readonly number[];
  children: ReactNode;
  rodape?: ReactNode;
}) {
  return (
    <div className="relative overflow-x-auto">
      <table className="w-full border-collapse text-(length:--fs-table) tnum sm:min-w-[560px]">
        <caption className="sr-only">{legenda}</caption>
        <thead>
          <tr>
            {cabecalhos.map((c, i) => (
              <th
                key={c}
                scope="col"
                className={cn(CABECALHO, direita.includes(i) ? "text-right" : "text-left")}
              >
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>{children}</tbody>
        {rodape && <tfoot>{rodape}</tfoot>}
      </table>
    </div>
  );
}

function LinhaTotal({
  rotulo,
  children,
  forte = false,
}: {
  rotulo: string;
  children: ReactNode;
  forte?: boolean;
}) {
  return (
    <tr>
      <th
        scope="row"
        colSpan={3}
        className={cn(
          "px-4 py-1.5 pl-6 text-left text-ink-2",
          forte ? "pt-3 pb-5 font-semibold text-ink" : "font-normal",
        )}
      >
        {rotulo}
      </th>
      <td className={cn("py-1.5 pr-6 pl-4 text-right text-ink", forte && "pt-3 pb-5")}>
        {children}
      </td>
    </tr>
  );
}

function TabelaCoberturas({ apolice }: { apolice: ApoliceDto }) {
  return (
    <Tabela
      legenda="Coberturas contratadas"
      cabecalhos={["Código", "Cobertura", "LMI", "Prêmio anual"]}
      direita={[2, 3]}
      rodape={
        <>
          <tr>
            <td colSpan={4} className="pt-3" />
          </tr>
          <LinhaTotal rotulo="Prêmio líquido na emissão">
            {formatarMoeda(apolice.premioLiquido)}
          </LinhaTotal>
          {apolice.ajustesEndosso !== 0 && (
            <LinhaTotal rotulo="Ajustes de endosso">
              <Money valor={apolice.ajustesEndosso} comSinal />
            </LinhaTotal>
          )}
          <LinhaTotal rotulo={`IOF (${formatarPercentual(apolice.aliquotaIof)})`}>
            {formatarMoeda(apolice.iof)}
          </LinhaTotal>
          <LinhaTotal rotulo="Prêmio total" forte>
            <Money valor={apolice.premioTotal} variante="heroi" className="type-fig-lg" />
          </LinhaTotal>
        </>
      }
    >
      {apolice.coberturas.map((c) => (
        <tr key={c.codigo} className="hover:bg-surface-hover">
          <td className={cn(CELULA, "mono-id font-semibold text-ink-2")}>{c.codigo}</td>
          <td className={CELULA}>
            <span className="text-ink">{c.nome}</span>
            <span className="block type-meta text-ink-3">
              {c.obrigatoria ? "Obrigatória" : "Opcional"}
              {!c.permiteSinistro && " · não permite sinistro"}
            </span>
          </td>
          <td className={cn(CELULA, "text-right text-ink")}>
            {c.lmi > 0 ? formatarMoeda(c.lmi) : "—"}
          </td>
          <td className={cn(CELULA, "text-right text-ink")}>{formatarMoeda(c.premioLiquido)}</td>
        </tr>
      ))}
    </Tabela>
  );
}

function AbaCoberturas({ apolice }: { apolice: ApoliceDto }) {
  return (
    <>
      <div className="hidden sm:block">
        <TabelaCoberturas apolice={apolice} />
      </div>
      <ListaCoberturasCompacta
        rotulo="Coberturas contratadas"
        linhas={apolice.coberturas.map((c) => ({
          codigo: c.codigo,
          nome: c.nome,
          lmi: c.lmi,
          premio: c.premioLiquido,
        }))}
        totais={[
          { rotulo: "Prêmio líquido na emissão", valor: formatarMoeda(apolice.premioLiquido) },
          ...(apolice.ajustesEndosso === 0
            ? []
            : [
                {
                  rotulo: "Ajustes de endosso",
                  valor: <Money valor={apolice.ajustesEndosso} comSinal />,
                },
              ]),
          {
            rotulo: `IOF (${formatarPercentual(apolice.aliquotaIof)})`,
            valor: formatarMoeda(apolice.iof),
          },
          { rotulo: "Prêmio total", valor: formatarMoeda(apolice.premioTotal), forte: true },
        ]}
      />
    </>
  );
}

type SituacaoParcela = "passado" | "proxima" | "avencer";

function situacoes(parcelas: readonly ParcelaDto[], hoje: string): SituacaoParcela[] {
  const proxima = parcelas.findIndex((p) => p.vencimento >= hoje);
  return parcelas.map((p, i) =>
    p.vencimento < hoje ? "passado" : i === proxima ? "proxima" : "avencer",
  );
}

/** Situação derivada da data: o domínio não modela pagamento de parcela (DESIGN.md §10.2). */
function SituacaoDaParcela({ situacao }: { situacao: SituacaoParcela }) {
  if (situacao === "proxima") return <Badge tom="info">Próxima</Badge>;
  if (situacao === "passado")
    return (
      <Badge tom="neutral" marcador={Clock}>
        Vencimento passado
      </Badge>
    );
  return (
    <Badge tom="neutral" marcador="plain">
      A vencer
    </Badge>
  );
}

function AbaParcelas({ apolice }: { apolice: ApoliceDto }) {
  const hoje = hojeEmBrasilia();
  const lista = situacoes(apolice.cronogramaParcelas, hoje);
  const total = apolice.cronogramaParcelas.length;
  const indiceHoje = lista.findIndex((s) => s !== "passado");
  return (
    <>
      <Tabela
        legenda="Cronograma de parcelas"
        cabecalhos={["Parcela", "Vencimento", "Valor", "Situação"]}
        direita={[2]}
      >
        {apolice.cronogramaParcelas.map((p, i) => (
          <ParcelaComMarcador key={p.numero} mostrarHoje={i === indiceHoje && indiceHoje > 0}>
            <tr
              className={cn(
                lista[i] === "proxima" && "bg-surface-selected shadow-[inset_2px_0_0_var(--link)]",
              )}
            >
              <td className={cn(CELULA, "text-ink")}>
                {p.numero}/{total}
              </td>
              <td className={cn(CELULA, "text-ink")}>
                <DateText valor={p.vencimento} />
              </td>
              <td className={cn(CELULA, "text-right text-ink")}>{formatarMoeda(p.valor)}</td>
              <td className={CELULA}>
                <SituacaoDaParcela situacao={lista[i] ?? "avencer"} />
              </td>
            </tr>
          </ParcelaComMarcador>
        ))}
      </Tabela>
      <p className="px-6 py-4 type-meta text-ink-3">
        A 1ª parcela absorve o ajuste de centavos. A situação vem da data de vencimento: o pagamento
        das parcelas não é registrado nesta plataforma.
      </p>
    </>
  );
}

function ParcelaComMarcador({
  mostrarHoje,
  children,
}: {
  mostrarHoje: boolean;
  children: ReactNode;
}) {
  return (
    <>
      {mostrarHoje && (
        <tr aria-hidden>
          <td colSpan={4} className="px-6 py-1">
            <span className="flex items-center gap-3 type-caption font-semibold text-gold-text">
              Hoje <span className="h-px flex-1 bg-gold-line/50" />
            </span>
          </td>
        </tr>
      )}
      {children}
    </>
  );
}

function AbaEndossos({ apolice }: { apolice: ApoliceDto }) {
  if (apolice.endossos.length === 0) {
    return <p className="px-6 py-6 text-sm text-ink-3">Nenhum endosso registrado nesta apólice.</p>;
  }
  return (
    <Tabela
      legenda="Endossos"
      cabecalhos={["Endosso", "Alteração", "Efeito", "Ajuste total"]}
      direita={[3]}
    >
      {apolice.endossos.map((e) => (
        <tr key={e.id}>
          <td className={cn(CELULA, "text-ink")}>
            nº {e.numero}
            <span className="block type-meta text-ink-3">por {e.criadoPorNome}</span>
          </td>
          <td className={CELULA}>
            <span className="text-ink">{ROTULO_TIPO_ENDOSSO[e.tipo]}</span>
            <span className="block type-meta text-ink-3">
              {e.coberturaNome}
              {e.lmiAnterior !== null &&
                e.lmiNovo !== null &&
                ` · LMI ${formatarMoeda(e.lmiAnterior)} → ${formatarMoeda(e.lmiNovo)}`}
            </span>
          </td>
          <td className={cn(CELULA, "text-ink")}>
            <DateText valor={e.dataEfeito} />
          </td>
          <td className={cn(CELULA, "text-right")}>
            <Money valor={e.ajusteTotal} comSinal className="font-semibold text-ink" />
            <span className="block type-meta text-ink-3">
              IOF <Money valor={e.iofAjuste} />
            </span>
          </td>
        </tr>
      ))}
    </Tabela>
  );
}

function AbaSinistros({
  apolice,
  aoAbrir,
}: {
  apolice: ApoliceDto;
  aoAbrir: (() => void) | undefined;
}) {
  return (
    <div>
      {apolice.sinistros.length === 0 ? (
        <p className="px-6 py-6 text-sm text-ink-3">Nenhum sinistro aberto nesta apólice.</p>
      ) : (
        <ul className="divide-y divide-line-soft border-t border-line">
          {apolice.sinistros.map((s) => (
            <li
              key={s.id}
              className="flex flex-wrap items-center justify-between gap-3 px-6 py-3.5"
            >
              <span className="min-w-0">
                <Link
                  to={`/sinistros/${s.id}`}
                  className="mono-id font-semibold text-link hover:underline"
                >
                  {s.numero}
                </Link>
                <span className="block type-meta text-ink-3">
                  {s.coberturaNome} · aberto em <DateText valor={s.abertoEm} />
                </span>
              </span>
              <span className="flex items-center gap-3">
                <Money valor={s.valorReclamado} className="text-sm text-ink" />
                <StatusBadge entidade="sinistro" status={s.status} />
              </span>
            </li>
          ))}
        </ul>
      )}
      {aoAbrir && (
        <div className="px-6 pt-2 pb-5">
          <Button variante="secundario" icone={LifeBuoy} onClick={aoAbrir}>
            Abrir sinistro
          </Button>
        </div>
      )}
    </div>
  );
}

function AbaComunicacoes({ apolice, aguardando }: { apolice: ApoliceDto; aguardando: boolean }) {
  const ordenadas = [...apolice.notificacoes].sort((a, b) => b.criadaEm.localeCompare(a.criadaEm));
  return (
    <div className="flex flex-col">
      {aguardando && (
        <p
          role="status"
          className="mx-6 mb-3 flex items-center gap-2 rounded-lg border border-info-line bg-info-bg px-3.5 py-2.5 text-label text-info"
        >
          <LoaderCircle
            size={15}
            className="motion-safe:animate-[girar_800ms_linear_infinite]"
            aria-hidden
          />
          Enviando as boas-vindas pela fila de mensagens…
        </p>
      )}
      {ordenadas.length === 0 && !aguardando ? (
        <p className="px-6 py-6 text-sm text-ink-3">Nenhum e-mail enviado ainda.</p>
      ) : (
        <ul className="divide-y divide-line-soft border-t border-line">
          {ordenadas.map((n) => (
            <li key={n.id} className="px-6 py-3.5">
              <details className="group">
                <summary className="flex cursor-pointer list-none items-start gap-3 rounded-md">
                  <span
                    aria-hidden
                    className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg bg-tl-steel-bg text-tl-steel"
                  >
                    <Mail size={15} />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-semibold text-ink">{n.assunto}</span>
                    <span className="block type-meta text-ink-3">
                      Para {n.destinatario} · <DateText valor={n.criadaEm} formato="data-hora" />
                    </span>
                  </span>
                  <Badge tom="neutral" marcador="plain">
                    {ROTULO_TIPO_NOTIFICACAO[n.tipo]}
                  </Badge>
                </summary>
                <p className="mt-3 ml-11 rounded-md border border-line bg-surface-2 px-3.5 py-3 text-label whitespace-pre-line text-ink-2">
                  {n.corpo}
                </p>
              </details>
            </li>
          ))}
        </ul>
      )}
      <p className="px-6 pt-3 pb-5 type-meta text-ink-3">
        E-mails simulados: a caixa de saída mostra o que seria enviado ao segurado.
      </p>
    </div>
  );
}

interface AbasApoliceProps {
  apolice: ApoliceDto;
  aguardandoBoasVindas: boolean;
  aoAbrirSinistro: (() => void) | undefined;
}

/** Abas com contadores (DESIGN.md §4.13): coberturas, parcelas, endossos, sinistros, comunicações. */
export function AbasApolice({ apolice, aguardandoBoasVindas, aoAbrirSinistro }: AbasApoliceProps) {
  return (
    <Card className="pt-1">
      <Tabs
        rotulo="Detalhes da apólice"
        className="[&_[role=tablist]]:px-4 md:[&_[role=tablist]]:px-5 [&_[role=tabpanel]]:pt-4"
        abas={[
          {
            valor: "coberturas",
            rotulo: "Coberturas",
            contador: apolice.coberturas.length,
            conteudo: <AbaCoberturas apolice={apolice} />,
          },
          {
            valor: "parcelas",
            rotulo: "Parcelas",
            contador: apolice.cronogramaParcelas.length,
            conteudo: <AbaParcelas apolice={apolice} />,
          },
          {
            valor: "endossos",
            rotulo: "Endossos",
            contador: apolice.endossos.length,
            conteudo: <AbaEndossos apolice={apolice} />,
          },
          {
            valor: "sinistros",
            rotulo: "Sinistros",
            contador: apolice.sinistros.length,
            conteudo: <AbaSinistros apolice={apolice} aoAbrir={aoAbrirSinistro} />,
          },
          {
            valor: "comunicacoes",
            rotulo: "Comunicações",
            contador: apolice.notificacoes.length,
            conteudo: <AbaComunicacoes apolice={apolice} aguardando={aguardandoBoasVindas} />,
          },
        ]}
      />
    </Card>
  );
}

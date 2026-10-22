import { ChevronRight } from "lucide-react";
import { Link } from "react-router";

import { useApolices } from "@/api/consultas/apolices";
import { LIMITE_MAXIMO_PAGINA } from "@/api/consultas/chaves";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";
import { SkeletonTexto } from "@/ui/componentes/Skeleton";
import { Money } from "@/ui/componentes/Valores";
import { hojeEmBrasilia, somarDias } from "@/ui/utils/datas";
import { formatarCompacto, formatarData, formatarNumero } from "@/ui/utils/formatar";

import { DIAS_JANELA_RENOVACAO, venceEmAte30Dias } from "../apolices/renovacao";
import { useTodasAsPaginas } from "../comum/useTodasAsPaginas";

const LIMITE_LISTA = 4;

export function Renovacoes({ className }: { className?: string }) {
  // A API não filtra por fim de vigência: lê as ativas até o fim (com teto) e filtra aqui.
  const consulta = useApolices({ status: "Ativa" }, LIMITE_MAXIMO_PAGINA);
  const progresso = useTodasAsPaginas(consulta, true);
  const hoje = hojeEmBrasilia();
  const ativas = consulta.data?.pages.flatMap((p) => p.itens) ?? [];
  const vencendo = ativas
    .filter((a) => venceEmAte30Dias(a, hoje))
    .sort((a, b) => a.vigenciaFim.localeCompare(b.vigenciaFim));
  const premio = vencendo.reduce((s, a) => s + a.premioTotal, 0);
  return (
    <Card className={className}>
      <CardHeader
        titulo="Renovações em 30 dias"
        subtitulo={`Vigências que terminam até ${formatarData(somarDias(hoje, DIAS_JANELA_RENOVACAO))}`}
      />
      <CardBody className="pt-3">
        {progresso.carregandoTudo ? (
          <SkeletonTexto linhas={4} />
        ) : (
          <>
            <p className="flex items-baseline gap-2 text-sm text-ink-2 tnum">
              <span className="font-serif text-3xl font-medium text-ink">
                {formatarNumero(vencendo.length)}
              </span>
              {vencendo.length === 1 ? "apólice" : "apólices"} · R$ {formatarCompacto(premio).texto}{" "}
              em prêmio
            </p>
            {progresso.truncado && (
              <p className="mt-1 type-meta text-ink-3">
                Entre as {formatarNumero(ativas.length)} apólices ativas mais recentes; as
                anteriores não foram lidas.
              </p>
            )}
            {vencendo.length === 0 ? (
              <p className="mt-3 text-sm text-ink-3">
                Nenhuma vigência termina nos próximos 30 dias.
              </p>
            ) : (
              <ul className="mt-3 divide-y divide-line-soft">
                {vencendo.slice(0, LIMITE_LISTA).map((a) => (
                  <li key={a.id} className="flex items-start justify-between gap-3 py-3">
                    <span className="min-w-0">
                      <Link
                        to={`/apolices/${a.id}`}
                        className="block truncate text-sm font-semibold text-ink hover:underline"
                      >
                        {a.seguradoNome}
                      </Link>
                      <span className="mono-id text-ink-3">{a.numero}</span>
                    </span>
                    <span className="shrink-0 text-right">
                      <Money
                        valor={a.premioTotal}
                        className="block text-sm font-semibold text-ink"
                      />
                      <span className="type-meta text-ink-3">
                        vence {formatarData(a.vigenciaFim).slice(0, 5)}
                      </span>
                    </span>
                  </li>
                ))}
              </ul>
            )}
            <Link
              to="/apolices?fila=renovacao-30d"
              className="mt-3 inline-flex h-10 w-full items-center justify-center gap-1 rounded-md border border-line text-sm font-semibold text-ink hover:border-line-strong hover:bg-surface-2"
            >
              Ver todas as renovações
              <ChevronRight size={15} aria-hidden />
            </Link>
          </>
        )}
      </CardBody>
    </Card>
  );
}

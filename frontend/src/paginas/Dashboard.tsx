import { Plus } from "lucide-react";
import { Link } from "react-router";

import { useDashboard } from "@/api/consultas/sistema";
import { pode } from "@/app/sessao/permissoes";
import { useUsuario } from "@/app/sessao/sessao-contexto";
import { Pagina } from "@/app/shell/Pagina";
import { Badge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";

import { ErroDeLista } from "./comum/EstadoDaConsulta";
import { AtividadeRecente } from "./dashboard/AtividadeRecente";
import { CartaoDistribuicao } from "./dashboard/CartaoDistribuicao";
import { CartaoPremio } from "./dashboard/CartaoPremio";
import { FaixaKpis } from "./dashboard/FaixaKpis";
import { Renovacoes } from "./dashboard/Renovacoes";

const fmtDiaPorExtenso = new Intl.DateTimeFormat("pt-BR", {
  weekday: "long",
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "America/Sao_Paulo",
});

function hojePorExtenso(): string {
  const texto = fmtDiaPorExtenso.format(new Date());
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

export default function Dashboard() {
  const usuario = useUsuario();
  const consulta = useDashboard();
  const global = consulta.data?.escopo === "Global";
  const podeCotar = pode(usuario.papel, "operarCarteira");

  return (
    <Pagina
      titulo="Visão da carteira"
      subtitulo={
        <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
          {hojePorExtenso()}
          {consulta.data && (
            <Badge tom={global ? "info" : "neutral"} marcador="plain">
              {global ? "Escopo global · todas as carteiras" : "Escopo: sua carteira"}
            </Badge>
          )}
        </span>
      }
      acoes={
        podeCotar && (
          <Button
            asChild
            icone={Plus}
            tamanho="lg"
            className="w-full md:h-10 md:w-auto md:px-4 md:text-sm"
          >
            <Link to="/cotacoes/nova">Nova cotação</Link>
          </Button>
        )
      }
    >
      {consulta.isError ? (
        <Card>
          <ErroDeLista
            erro={consulta.error}
            oQue="os indicadores da carteira"
            aoTentarDeNovo={() => void consulta.refetch()}
          />
        </Card>
      ) : (
        <>
          <FaixaKpis dados={consulta.data} />
          <div className="grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-12">
            <CartaoPremio dados={consulta.data} className="lg:col-span-8" />
            <CartaoDistribuicao dados={consulta.data} className="lg:col-span-4" />
            {/* Mobile: renovações antes da atividade (DESIGN.md §5.3). */}
            <AtividadeRecente className="order-2 lg:order-none lg:col-span-8" />
            <Renovacoes className="order-1 lg:order-none lg:col-span-4" />
          </div>
        </>
      )}
    </Pagina>
  );
}

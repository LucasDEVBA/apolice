import { Plus } from "lucide-react";
import { Link } from "react-router";

import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";
import { usePersona } from "@/app/persona-contexto";
import { Button } from "@/ui/componentes/Button";

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
  const { persona } = usePersona();
  return (
    <Pagina
      titulo="Visão da carteira"
      subtitulo={`${hojePorExtenso()} · Ribeiro & Associados Corretora`}
      acoes={
        persona.papel !== "Analista" && (
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
      <EmConstrucao descricao="KPIs da carteira, prêmio emitido em 12 meses, distribuição por produto, renovações e atividade recente chegam com a integração do GET /dashboard." />
    </Pagina>
  );
}

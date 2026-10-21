import { Plus } from "lucide-react";
import { Link } from "react-router";

import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";
import { Button } from "@/ui/componentes/Button";

export default function Cotacoes() {
  return (
    <Pagina
      titulo="Cotações"
      subtitulo="Cotações calculadas, aceitas e emitidas da sua carteira"
      acoes={
        <Button asChild icone={Plus}>
          <Link to="/cotacoes/nova">Nova cotação</Link>
        </Button>
      }
    >
      <EmConstrucao descricao="A lista com busca, filtros de status e produto e paginação por cursor chega com o GET /cotacoes." />
    </Pagina>
  );
}

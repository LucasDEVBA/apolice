import { FileSearch } from "lucide-react";
import { Link } from "react-router";

import { Pagina } from "@/app/shell/Pagina";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { EmptyState } from "@/ui/componentes/EmptyState";

export default function NaoEncontrada() {
  return (
    <Pagina titulo="Página não encontrada">
      <Card>
        <EmptyState
          icone={FileSearch}
          titulo="Nada por aqui"
          descricao="O endereço pode ter mudado ou o registro não existe na sua carteira."
          acao={
            <Button asChild>
              <Link to="/">Voltar ao Dashboard</Link>
            </Button>
          }
        />
      </Card>
    </Pagina>
  );
}

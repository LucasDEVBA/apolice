import { useParams } from "react-router";

import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";

export default function SinistroDetalhe() {
  const { id = "" } = useParams();
  return (
    <Pagina
      titulo={`Sinistro ${id}`}
      migalhas={[
        { rotulo: "Sinistros", href: "/sinistros" },
        { rotulo: id, codigo: true },
      ]}
    >
      <EmConstrucao descricao="A linha do tempo do histórico, a triagem automática e as ações do analista (analisar, aprovar, negar, registrar pagamento) chegam com o GET /sinistros/{id}." />
    </Pagina>
  );
}

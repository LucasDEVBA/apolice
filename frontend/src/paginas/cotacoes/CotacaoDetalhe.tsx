import { useParams } from "react-router";

import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";

export default function CotacaoDetalhe() {
  const { id = "" } = useParams();
  return (
    <Pagina
      titulo={`Cotação ${id}`}
      migalhas={[
        { rotulo: "Cotações", href: "/cotacoes" },
        { rotulo: id, codigo: true },
      ]}
    >
      <EmConstrucao descricao="Itens, fatores, validade e as ações Aceitar, Recusar e Emitir apólice chegam com o GET /cotacoes/{id}." />
    </Pagina>
  );
}

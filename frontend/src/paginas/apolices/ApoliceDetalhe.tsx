import { useParams } from "react-router";

import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";

export default function ApoliceDetalhe() {
  const { id = "" } = useParams();
  return (
    <Pagina
      titulo={`Apólice ${id}`}
      migalhas={[
        { rotulo: "Apólices", href: "/apolices" },
        { rotulo: id, codigo: true },
      ]}
    >
      <EmConstrucao descricao="A face da apólice, as abas (coberturas, parcelas, endossos, sinistros, auditoria) e as ações de PDF, endosso e cancelamento com simulação chegam com o GET /apolices/{id}." />
    </Pagina>
  );
}

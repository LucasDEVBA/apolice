import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";

export default function NovaCotacao() {
  return (
    <Pagina
      titulo="Nova cotação"
      migalhas={[{ rotulo: "Cotações", href: "/cotacoes" }, { rotulo: "Nova cotação" }]}
    >
      <EmConstrucao descricao="O assistente em 5 etapas (Produto, Segurado, Risco, Coberturas e Revisão) com memória de cálculo ao vivo chega com o POST /cotacoes/simular." />
    </Pagina>
  );
}

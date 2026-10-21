import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";

export default function Sinistros() {
  return (
    <Pagina titulo="Sinistros" subtitulo="Avisos de sinistro, triagem e regulação">
      <EmConstrucao descricao="A fila com score de triagem, status e valor reclamado chega com o GET /sinistros." />
    </Pagina>
  );
}

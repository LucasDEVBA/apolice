import { EmConstrucao } from "@/app/shell/EmConstrucao";
import { Pagina } from "@/app/shell/Pagina";

export default function Apolices() {
  return (
    <Pagina titulo="Apólices" subtitulo="Apólices emitidas, programadas, vencidas e canceladas">
      <EmConstrucao descricao="A lista com status efetivo, vigência e CPF mascarado chega com o GET /apolices." />
    </Pagina>
  );
}

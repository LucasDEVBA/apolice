import { EmConstrucao } from "@/app/shell/EmConstrucao";

import { AreaAdmin } from "./AreaAdmin";

export default function AdminTarifas() {
  return (
    <AreaAdmin
      titulo="Tarifas"
      subtitulo="Taxas por cobertura dos produtos Auto, Residencial e Vida"
    >
      <EmConstrucao descricao="A edição de taxa por cobertura (PUT /produtos/{codigo}/coberturas/{coberturaCodigo}/taxa), com auditoria, chega na fase 2." />
    </AreaAdmin>
  );
}

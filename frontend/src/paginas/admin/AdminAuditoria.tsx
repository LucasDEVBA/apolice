import { EmConstrucao } from "@/app/shell/EmConstrucao";

import { AreaAdmin } from "./AreaAdmin";

export default function AdminAuditoria() {
  return (
    <AreaAdmin titulo="Auditoria" subtitulo="Trilha de ações sensíveis com ID de correlação">
      <EmConstrucao descricao="A trilha paginada por cursor (GET /admin/auditoria) chega na fase 2." />
    </AreaAdmin>
  );
}

import { Construction } from "lucide-react";
import type { ReactNode } from "react";

import { Card } from "@/ui/componentes/Card";
import { EmptyState } from "@/ui/componentes/EmptyState";

/** Placeholder honesto das telas da fase 1: diz o que vem na integração com a API. */
export function EmConstrucao({ descricao, acao }: { descricao: ReactNode; acao?: ReactNode }) {
  return (
    <Card>
      <EmptyState
        icone={Construction}
        titulo="Em construção"
        descricao={descricao}
        {...(acao ? { acao } : {})}
      />
    </Card>
  );
}

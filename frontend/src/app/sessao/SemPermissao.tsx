import { Lock } from "lucide-react";
import { Link } from "react-router";

import { Pagina } from "@/app/shell/Pagina";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { EmptyState } from "@/ui/componentes/EmptyState";

import { PERFIS_DA_PERMISSAO, type Permissao } from "./permissoes";

/** 403 amigável: diz qual perfil acessa e como trocar de persona na demonstração. */
export function SemPermissao({ permissao }: { permissao: Permissao }) {
  return (
    <Pagina titulo="Acesso restrito">
      <Card>
        <EmptyState
          icone={Lock}
          titulo={`Disponível para o perfil ${PERFIS_DA_PERMISSAO[permissao]}`}
          descricao="Seu perfil não tem permissão para esta tela. Na demonstração, troque de persona pelo menu do usuário, no rodapé da navegação."
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

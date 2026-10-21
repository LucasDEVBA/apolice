import { Lock } from "lucide-react";
import type { ReactNode } from "react";
import { NavLink } from "react-router";

import { usePersona } from "@/app/persona-contexto";
import { Pagina } from "@/app/shell/Pagina";
import { Card } from "@/ui/componentes/Card";
import { EmptyState } from "@/ui/componentes/EmptyState";

const SECOES = [
  { para: "/admin/tarifas", rotulo: "Tarifas" },
  { para: "/admin/auditoria", rotulo: "Auditoria" },
] as const;

/** Moldura do Admin: sub-navegação e trava de papel (o backend também exige a política Admin). */
export function AreaAdmin({
  titulo,
  subtitulo,
  children,
}: {
  titulo: string;
  subtitulo: string;
  children: ReactNode;
}) {
  const { persona } = usePersona();
  return (
    <Pagina
      titulo={titulo}
      subtitulo={subtitulo}
      // A página atual da trilha é a seção (o H1), não "Admin".
      migalhas={[{ rotulo: "Admin", href: "/admin/tarifas" }, { rotulo: titulo }]}
    >
      <nav aria-label="Seções do Admin" className="border-b border-line">
        <ul className="flex gap-2">
          {SECOES.map((secao) => (
            <li key={secao.para}>
              <NavLink
                to={secao.para}
                className="-mb-px inline-flex h-11 items-center border-b-2 border-transparent px-3 text-sm font-medium text-ink-2 hover:text-ink aria-[current=page]:border-primary aria-[current=page]:font-semibold aria-[current=page]:text-ink"
              >
                {secao.rotulo}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      {persona.papel === "Admin" ? (
        children
      ) : (
        <Card>
          <EmptyState
            icone={Lock}
            titulo="Disponível para o perfil Admin"
            descricao="Troque para a persona Diego Martins (Admin) no menu do usuário para ver esta área."
          />
        </Card>
      )}
    </Pagina>
  );
}

import type { ReactNode } from "react";
import { NavLink } from "react-router";

import { Pagina } from "@/app/shell/Pagina";

const SECOES = [
  { para: "/admin/tarifas", rotulo: "Tarifas" },
  { para: "/admin/auditoria", rotulo: "Auditoria" },
] as const;

/** Moldura do Admin: sub-navegação. A trava de papel fica na rota (e a API exige a política Admin). */
export function AreaAdmin({
  titulo,
  subtitulo,
  children,
}: {
  titulo: string;
  subtitulo: string;
  children: ReactNode;
}) {
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
      {children}
    </Pagina>
  );
}

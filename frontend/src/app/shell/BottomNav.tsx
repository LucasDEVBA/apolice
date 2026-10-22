import { Ellipsis } from "lucide-react";
import { NavLink, useLocation } from "react-router";

import { NAVEGACAO_PRINCIPAL } from "../navegacao";

const ITEM =
  "flex h-full min-w-11 flex-col items-center justify-center gap-1 type-micro text-ink-3 " +
  "aria-[current=page]:font-bold aria-[current=page]:text-ink [&[aria-current=page]_svg]:text-primary";

/** Bottom nav (< md): Início, Cotações, Apólices, Sinistros e "Mais", que abre o drawer (DESIGN.md §5.2). */
export function BottomNav({ aoAbrirMais }: { aoAbrirMais: () => void }) {
  // Na cotação mobile a bottom nav dá lugar à barra do prêmio total (DESIGN.md §5.2).
  if (useLocation().pathname === "/cotacoes/nova") return null;
  return (
    <nav
      aria-label="Principal"
      className="fixed inset-x-0 bottom-0 z-30 h-[calc(var(--bottom-nav-h)+env(safe-area-inset-bottom))] border-t border-line bg-surface pb-[env(safe-area-inset-bottom)] md:hidden"
    >
      <ul className="grid h-(--bottom-nav-h) grid-cols-5">
        {NAVEGACAO_PRINCIPAL.map((item) => {
          const Icone = item.icone;
          return (
            <li key={item.para}>
              <NavLink to={item.para} end={item.exato ?? false} className={ITEM}>
                <Icone size={20} aria-hidden />
                {item.rotuloCurto ?? item.rotulo}
              </NavLink>
            </li>
          );
        })}
        <li>
          <button
            type="button"
            onClick={aoAbrirMais}
            className={`${ITEM} w-full`}
            aria-haspopup="dialog"
          >
            <Ellipsis size={20} aria-hidden />
            Mais
          </button>
        </li>
      </ul>
    </nav>
  );
}

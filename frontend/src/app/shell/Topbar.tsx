import { BookOpen, CircleHelp, Info, Keyboard, Menu, Search } from "lucide-react";
import { DropdownMenu as RadixMenu } from "radix-ui";
import { Link } from "react-router";

import { Avatar } from "@/ui/componentes/Avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
} from "@/ui/componentes/DropdownMenu";
import { IconButton } from "@/ui/componentes/IconButton";
import { ThemeToggle } from "@/ui/componentes/ThemeToggle";
import { Marca } from "@/ui/marca/Marca";

import { usePersona } from "../persona-contexto";
import { Notificacoes } from "./Notificacoes";
import { MenuPersona } from "./Sidebar";

const EH_MAC = typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.userAgent);
const ATALHO_BUSCA = EH_MAC ? "⌘K" : "Ctrl K";

/** Selo "Demonstração": dado de demonstração é sempre rotulado (DESIGN.md §8). */
function SeloDemonstracao() {
  return (
    <span className="inline-flex h-[26px] items-center rounded-full border border-demo-line bg-demo-bg px-2.5 text-meta font-semibold text-demo">
      Demonstração
    </span>
  );
}

function MenuAjuda() {
  return (
    <DropdownMenu>
      <RadixMenu.Trigger
        aria-label="Ajuda"
        className="grid size-10 place-items-center rounded-md text-ink-2 hover:bg-hover-tint hover:text-ink"
      >
        <CircleHelp size={18} aria-hidden />
      </RadixMenu.Trigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel>Ajuda</DropdownMenuLabel>
        <DropdownMenuItem asChild>
          <Link to="/sobre">
            <Info size={15} className="text-ink-2" aria-hidden />
            Sobre o projeto
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <a href="/scalar/v1" target="_blank" rel="noreferrer">
            <BookOpen size={15} className="text-ink-2" aria-hidden />
            Documentação da API
            <span className="sr-only">(abre em nova aba)</span>
          </a>
        </DropdownMenuItem>
        <DropdownMenuItem disabled>
          <Keyboard size={15} className="text-ink-3" aria-hidden />
          Buscar: {ATALHO_BUSCA}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/**
 * Topbar de 64 px (md+): gatilho da paleta de busca à esquerda; selo, ajuda, notificações e
 * aparência à direita. Quem é o usuário fica no rodapé da sidebar (DESIGN.md §4.3).
 */
export function Topbar({ aoBuscar }: { aoBuscar: () => void }) {
  return (
    <header className="flex h-16 items-center gap-4 border-b border-line bg-surface px-6 xl:px-10">
      <button
        type="button"
        onClick={aoBuscar}
        aria-keyshortcuts={EH_MAC ? "Meta+K" : "Control+K"}
        className="flex h-10 w-full max-w-[420px] min-w-0 items-center gap-2.5 rounded-md border border-line bg-surface-2 px-3 text-left text-sm text-ink-3 hover:border-line-strong"
      >
        <Search size={16} className="shrink-0" aria-hidden />
        <span className="truncate">Buscar apólice, segurado, CPF ou placa</span>
        <kbd className="ml-auto shrink-0 rounded-xs border border-line bg-surface px-1.5 font-sans text-[0.71875rem] font-semibold text-ink-2">
          {ATALHO_BUSCA}
        </kbd>
      </button>
      <div className="ml-auto flex items-center gap-2">
        <SeloDemonstracao />
        <MenuAjuda />
        <Notificacoes />
        <ThemeToggle comDensidade />
      </div>
    </header>
  );
}

/** Top bar marinho de 56 px (< md): ☰, marca, busca, sino e persona (DESIGN.md §5.1). */
export function TopbarMobile({
  aoAbrirMenu,
  aoBuscar,
}: {
  aoAbrirMenu: () => void;
  aoBuscar: () => void;
}) {
  const { persona } = usePersona();
  return (
    <header className="on-navy flex h-14 items-center gap-0.5 bg-sidebar pr-2 pl-1 text-on-navy">
      <IconButton
        icone={Menu}
        rotulo="Abrir menu"
        variante="marinho"
        tamanho="lg"
        onClick={aoAbrirMenu}
      />
      <Marca className="ml-0.5 min-w-0" />
      <div className="ml-auto flex items-center gap-0.5">
        <IconButton
          icone={Search}
          rotulo="Buscar"
          variante="marinho"
          tamanho="lg"
          onClick={aoBuscar}
        />
        <Notificacoes sobreMarinho />
        <MenuPersona
          gatilho={
            <button type="button" className="grid size-11 place-items-center rounded-full">
              {/* Nome pelo conteúdo (não aria-label): as iniciais visíveis não conflitam com o nome (WCAG 2.5.3). */}
              <Avatar nome={persona.nome} variante="navy" anel />
              <span className="sr-only">
                Trocar persona (atual: {persona.nome}, {persona.rotuloPapel})
              </span>
            </button>
          }
        />
      </div>
    </header>
  );
}

import { Suspense, useEffect, useState } from "react";
import { Outlet, useLocation } from "react-router";

import { PreferenciasAparencia } from "@/ui/componentes/ThemeToggle";
import { Drawer } from "@/ui/componentes/Dialog";

import { useUsuario } from "../sessao/sessao-contexto";
import { BottomNav } from "./BottomNav";
import { CarregandoPagina } from "./CarregandoPagina";
import { PaletaComandos } from "./PaletaComandos";
import { ConteudoSidebar, SidebarCompleta, SidebarRail } from "./Sidebar";
import { Topbar, TopbarMobile } from "./Topbar";

/**
 * Shell autenticado (DESIGN.md §5.1): sidebar de 248 px (xl), rail de 72 px (md–xl) e, no mobile,
 * top bar marinho + bottom nav. "Pular para o conteúdo" é o primeiro foco da página.
 */
export function AppShell() {
  const [menuAberto, setMenuAberto] = useState(false);
  const [buscaAberta, setBuscaAberta] = useState(false);
  const local = useLocation();
  const usuario = useUsuario();

  // ⌘K / Ctrl K abre a paleta de comandos de qualquer lugar.
  useEffect(() => {
    function aoTeclar(evento: KeyboardEvent) {
      // O autofill do Chrome dispara keydown sem "key"; composição (IME) também não é atalho.
      // (o tipo do DOM promete string; o valor real pode vir undefined).
      const tecla: unknown = evento.key;
      if (typeof tecla !== "string" || evento.isComposing) return;
      if (tecla.toLowerCase() === "k" && (evento.metaKey || evento.ctrlKey)) {
        evento.preventDefault();
        setBuscaAberta(true);
      }
    }
    window.addEventListener("keydown", aoTeclar);
    return () => window.removeEventListener("keydown", aoTeclar);
  }, []);

  return (
    <div className="min-h-dvh bg-canvas md:grid md:grid-cols-[72px_minmax(0,1fr)] xl:grid-cols-[248px_minmax(0,1fr)]">
      <a
        href="#conteudo"
        className="fixed top-2 left-2 z-[60] -translate-y-24 rounded-md bg-primary px-4 py-2.5 text-sm font-semibold text-primary-fg shadow-overlay transition-transform focus-visible:translate-y-0"
      >
        Pular para o conteúdo
      </a>

      <SidebarRail aoAbrirMenu={() => setMenuAberto(true)} className="hidden md:flex xl:hidden" />
      <SidebarCompleta className="hidden xl:block" />

      <div className="flex min-w-0 flex-col">
        {/* O sticky fica no wrapper: o header sozinho só grudaria dentro do próprio pai. */}
        <div className="sticky top-0 z-20 md:hidden">
          <TopbarMobile
            aoAbrirMenu={() => setMenuAberto(true)}
            aoBuscar={() => setBuscaAberta(true)}
          />
        </div>
        <div className="sticky top-0 z-20 hidden md:block">
          <Topbar aoBuscar={() => setBuscaAberta(true)} />
        </div>
        <main
          id="conteudo"
          tabIndex={-1}
          className="flex-1 px-4 pt-5 pb-[calc(var(--bottom-nav-h)+env(safe-area-inset-bottom)+24px)] outline-none md:px-6 md:pt-6 md:pb-12 xl:px-10 xl:pt-8"
        >
          <div className="mx-auto w-full max-w-[1360px]">
            {/* key: cada rota tem a própria fronteira de Suspense (o fallback não "pisca" a anterior).
                O usuário entra na key: trocada a sessão (persona, outra aba), a página remonta e
                nenhum estado local (formulário, rascunho, diálogo) do usuário anterior sobrevive. */}
            <Suspense key={`${usuario.id}:${local.pathname}`} fallback={<CarregandoPagina />}>
              <Outlet />
            </Suspense>
          </div>
        </main>
      </div>

      <BottomNav aoAbrirMais={() => setMenuAberto(true)} />

      <Drawer aberto={menuAberto} aoMudar={setMenuAberto} titulo="Menu">
        <ConteudoSidebar
          aoNavegar={() => setMenuAberto(false)}
          rodapeExtra={
            <div className="mt-5 border-t border-line-navy px-2 pt-4 pb-3">
              <PreferenciasAparencia />
            </div>
          }
        />
      </Drawer>

      <PaletaComandos aberta={buscaAberta} aoMudar={setBuscaAberta} />
    </div>
  );
}

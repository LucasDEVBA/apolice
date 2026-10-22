import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router";

import { Marca } from "@/ui/marca/Marca";
import { RegiaoCarregando, Skeleton } from "@/ui/componentes/Skeleton";

import { pode, type Permissao } from "./permissoes";
import { useSessao } from "./sessao-contexto";
import { SemPermissao } from "./SemPermissao";

export interface EstadoNavegacaoLogin {
  /** Para onde voltar depois de entrar. */
  de?: string;
}

/** Tela neutra enquanto o refresh do load decide se há sessão (evita piscar o login). */
function VerificandoSessao() {
  return (
    <RegiaoCarregando
      carregando
      rotulo="Verificando a sessão…"
      className="grid min-h-dvh place-items-center bg-canvas"
    >
      <div className="flex flex-col items-center gap-4 text-ink">
        <Marca />
        <Skeleton className="h-1 w-32 rounded-full" />
      </div>
    </RegiaoCarregando>
  );
}

/** Shell autenticado: sem sessão, vai para /login guardando a rota de origem. */
export function RotaProtegida({ children }: { children: ReactNode }) {
  const { estado } = useSessao();
  const local = useLocation();
  if (estado.status === "verificando") return <VerificandoSessao />;
  if (estado.status === "anonima") {
    const destino: EstadoNavegacaoLogin = { de: `${local.pathname}${local.search}` };
    return <Navigate to="/login" replace state={destino} />;
  }
  return children;
}

/** Papel insuficiente: página 403 amigável no lugar da tela (a API também recusaria). */
export function RequerPermissao({
  permissao,
  children,
}: {
  permissao: Permissao;
  children: ReactNode;
}) {
  const { estado } = useSessao();
  const papel = estado.status === "autenticada" ? estado.usuario.papel : undefined;
  return pode(papel, permissao) ? children : <SemPermissao permissao={permissao} />;
}

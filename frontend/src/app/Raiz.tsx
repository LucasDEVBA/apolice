import { Suspense } from "react";
import { Outlet } from "react-router";

import { ProvedorAnuncioDeRota } from "./ProvedorAnuncioDeRota";
import { CarregandoPagina } from "./shell/CarregandoPagina";

/**
 * Raiz das rotas: anúncio de rota de toda a sessão e fronteira de Suspense para as telas fora do
 * shell (login, vitrine).
 */
export function Raiz() {
  return (
    <ProvedorAnuncioDeRota>
      <Suspense
        fallback={
          <div className="p-6">
            <CarregandoPagina />
          </div>
        }
      >
        <Outlet />
      </Suspense>
    </ProvedorAnuncioDeRota>
  );
}

import { lazy, type ComponentType } from "react";
import type { RouteObject } from "react-router";

import { Raiz } from "./Raiz";
import { AppShell } from "./shell/AppShell";
import { ErroDeRota } from "./shell/ErroDeRota";

type Modulo = Promise<{ default: ComponentType }>;

/** Cada tela vira um chunk próprio (React.lazy); o Suspense fica no shell/raiz. */
function pagina(carregar: () => Modulo): RouteObject["element"] {
  const Componente = lazy(carregar);
  return <Componente />;
}

const erro = <ErroDeRota />;

function rotaDoShell(path: string | undefined, carregar: () => Modulo): RouteObject {
  return path === undefined
    ? { index: true, element: pagina(carregar), errorElement: erro }
    : { path, element: pagina(carregar), errorElement: erro };
}

// Vitrine de componentes só existe em desenvolvimento: o import some do build de produção.
const rotasDeDesenvolvimento: RouteObject[] = import.meta.env.DEV
  ? [
      {
        path: "_componentes",
        element: pagina(() => import("@/paginas/vitrine/Vitrine")),
        errorElement: erro,
      },
    ]
  : [];

export const rotas: RouteObject[] = [
  {
    element: <Raiz />,
    errorElement: erro,
    children: [
      { path: "login", element: pagina(() => import("@/paginas/Login")), errorElement: erro },
      ...rotasDeDesenvolvimento,
      {
        path: "/",
        element: <AppShell />,
        errorElement: erro,
        children: [
          rotaDoShell(undefined, () => import("@/paginas/Dashboard")),
          rotaDoShell("cotacoes", () => import("@/paginas/cotacoes/Cotacoes")),
          rotaDoShell("cotacoes/nova", () => import("@/paginas/cotacoes/NovaCotacao")),
          rotaDoShell("cotacoes/:id", () => import("@/paginas/cotacoes/CotacaoDetalhe")),
          rotaDoShell("apolices", () => import("@/paginas/apolices/Apolices")),
          rotaDoShell("apolices/:id", () => import("@/paginas/apolices/ApoliceDetalhe")),
          rotaDoShell("sinistros", () => import("@/paginas/sinistros/Sinistros")),
          rotaDoShell("sinistros/:id", () => import("@/paginas/sinistros/SinistroDetalhe")),
          rotaDoShell("admin/tarifas", () => import("@/paginas/admin/AdminTarifas")),
          rotaDoShell("admin/auditoria", () => import("@/paginas/admin/AdminAuditoria")),
          rotaDoShell("sobre", () => import("@/paginas/Sobre")),
          rotaDoShell("*", () => import("@/paginas/NaoEncontrada")),
        ],
      },
    ],
  },
];

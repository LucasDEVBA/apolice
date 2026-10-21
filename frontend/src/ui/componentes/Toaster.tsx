import type { CSSProperties } from "react";
import { Toaster as Sonner } from "sonner";

import { useMediaQuery } from "../tema/useMediaQuery";
import { useTema } from "../tema/preferencias";

const MAXIMO_EMPILHADOS = 3;
const MARGEM_DESKTOP = 24;
const MARGEM_LATERAL_MOBILE = 16;
// Acima da bottom nav (64 + 12) e da área segura do iPhone (DESIGN.md §4.18).
const ACIMA_DA_BOTTOM_NAV = "calc(var(--bottom-nav-h) + 12px + env(safe-area-inset-bottom))";

/**
 * Toaster único do app: canto inferior direito a 24 px. Abaixo de 768 px (onde existe a bottom
 * nav) fica acima dela. O "mobile" do sonner começa em 600 px; por isso o deslocamento acompanha
 * o breakpoint do shell, e não o do sonner.
 */
export function Toaster() {
  const { resolvido } = useTema();
  const comBottomNav = !useMediaQuery("(width >= 48rem)");
  const deslocamento = comBottomNav
    ? { bottom: ACIMA_DA_BOTTOM_NAV, left: MARGEM_LATERAL_MOBILE, right: MARGEM_LATERAL_MOBILE }
    : MARGEM_DESKTOP;
  return (
    <Sonner
      theme={resolvido === "escuro" ? "dark" : "light"}
      position="bottom-right"
      offset={deslocamento}
      mobileOffset={{
        bottom: ACIMA_DA_BOTTOM_NAV,
        left: MARGEM_LATERAL_MOBILE,
        right: MARGEM_LATERAL_MOBILE,
      }}
      visibleToasts={MAXIMO_EMPILHADOS}
      containerAriaLabel="Notificações"
      toastOptions={{ unstyled: true, className: "w-full" }}
      // Largura do toast do guia (380 px); no mobile o sonner ocupa a largura menos as margens.
      style={{ "--width": "380px" } as CSSProperties}
    />
  );
}

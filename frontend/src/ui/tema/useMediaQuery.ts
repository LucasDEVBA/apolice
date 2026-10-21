import { useCallback, useSyncExternalStore } from "react";

/** true enquanto a media query casar (ex.: "(width >= 48rem)"). */
export function useMediaQuery(consulta: string): boolean {
  const assinar = useCallback(
    (aoMudar: () => void) => {
      if (typeof window.matchMedia !== "function") return () => undefined;
      const lista = window.matchMedia(consulta);
      lista.addEventListener("change", aoMudar);
      return () => lista.removeEventListener("change", aoMudar);
    },
    [consulta],
  );
  const ler = useCallback(
    () => typeof window.matchMedia === "function" && window.matchMedia(consulta).matches,
    [consulta],
  );
  return useSyncExternalStore(assinar, ler, () => false);
}

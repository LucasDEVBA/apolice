import { cn } from "../utils/cn";
import anelVazio from "./gravuras/anel-vazio.svg";
import rosetaLg from "./gravuras/roseta-lg.svg";
import rosetaSm from "./gravuras/roseta-sm.svg";
import rosetaXl from "./gravuras/roseta-xl.svg";
import selo from "./gravuras/selo.svg";

interface MarcaProps {
  /** Tamanho do selo em px (30 na sidebar, 26 na top bar mobile, 36 no login). */
  tamanhoSelo?: number;
  /** Classe da palavra "Apólice" (cor e tamanho). */
  classePalavra?: string;
  className?: string;
}

/** Selo gravado (roseta de 8 lóbulos em folha dourada) + "Apólice" em serifa 600 (DESIGN.md §4.1). */
export function Marca({ tamanhoSelo = 30, classePalavra, className }: MarcaProps) {
  return (
    <span className={cn("inline-flex items-center gap-2.5", className)}>
      <img src={selo} alt="" width={tamanhoSelo} height={tamanhoSelo} className="shrink-0" />
      <span
        className={cn(
          "font-serif text-[1.375rem] leading-none font-semibold tracking-[-0.01em]",
          classePalavra,
        )}
      >
        Apólice
      </span>
    </span>
  );
}

const GRAVURAS = { xl: rosetaXl, lg: rosetaLg, sm: rosetaSm, anel: anelVazio } as const;

interface GravuraProps {
  tamanho: keyof typeof GRAVURAS;
  className?: string;
}

/**
 * Gravura guilloché decorativa, gerada em build (scripts/gerar-gravuras.mjs). Só sobre marinho
 * ou no anel do estado vazio, e no máximo uma por viewport.
 */
export function Gravura({ tamanho, className }: GravuraProps) {
  return (
    <img
      src={GRAVURAS[tamanho]}
      alt=""
      aria-hidden
      decoding="async"
      loading={tamanho === "xl" ? "eager" : "lazy"}
      className={cn("pointer-events-none select-none", className)}
    />
  );
}

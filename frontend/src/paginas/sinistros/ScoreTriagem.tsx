import { Badge, type Tom } from "@/ui/componentes/Badge";

const LIMITE_ALTO = 60;
const LIMITE_MEDIO = 30;

function tomDoScore(score: number): { tom: Tom; rotulo: string } {
  if (score >= LIMITE_ALTO) return { tom: "bad", rotulo: "risco alto" };
  if (score >= LIMITE_MEDIO) return { tom: "warn", rotulo: "atenção" };
  return { tom: "ok", rotulo: "risco baixo" };
}

/** Score da triagem (0–100) com rótulo de faixa, nunca só pela cor. */
export function ScoreTriagem({
  score,
  compacto = false,
}: {
  score: number | null;
  compacto?: boolean;
}) {
  if (score === null)
    return <span className="text-ink-3">{compacto ? "—" : "Aguardando triagem"}</span>;
  const { tom, rotulo } = tomDoScore(score);
  return (
    <Badge tom={tom} marcador="plain" title={`Score de triagem ${String(score)} de 100: ${rotulo}`}>
      {score}
      {!compacto && <span className="font-medium"> · {rotulo}</span>}
      {compacto && <span className="sr-only"> de 100, {rotulo}</span>}
    </Badge>
  );
}

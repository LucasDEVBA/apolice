import { RegiaoCarregando, Skeleton } from "@/ui/componentes/Skeleton";

/** Fallback do Suspense de rota: esqueleto do cabeçalho e de um card, com aria-busy. */
export function CarregandoPagina() {
  return (
    <RegiaoCarregando carregando rotulo="Carregando página…" className="flex flex-col gap-6">
      <div className="flex flex-col gap-3">
        <Skeleton className="h-8 w-64 max-w-full" />
        <Skeleton className="h-3 w-80 max-w-full" />
      </div>
      <Skeleton className="h-64 w-full rounded-xl" />
    </RegiaoCarregando>
  );
}

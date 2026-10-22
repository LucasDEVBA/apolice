import type { ReactNode } from "react";

/**
 * Rodapé de diálogo com formulário: o mesmo visual do rodapé do Dialog (--surface-2, ações à
 * direita, principal em cima no mobile), mas DENTRO do <form>, para o submit funcionar com Enter.
 */
export function RodapeDoDialogo({ children }: { children: ReactNode }) {
  return (
    <div className="-mx-4 mt-6 -mb-6 flex flex-col-reverse gap-2 border-t border-line bg-surface-2 px-4 py-4 md:-mx-6 md:flex-row md:justify-end md:px-6 [&>*]:w-full md:[&>*]:w-auto">
      {children}
    </div>
  );
}

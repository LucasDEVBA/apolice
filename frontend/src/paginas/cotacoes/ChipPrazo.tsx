import { Badge } from "@/ui/componentes/Badge";

import { textoDoPrazo } from "./prazo";

export function ChipPrazo({ validaAte }: { validaAte: string }) {
  return (
    <Badge tom="warn" marcador="plain">
      {textoDoPrazo(validaAte)}
    </Badge>
  );
}

import { Monitor, Moon, Sun } from "lucide-react";
import { DropdownMenu as RadixMenu } from "radix-ui";
import { useId } from "react";

import {
  definirDensidade,
  definirPreferenciaTema,
  useDensidade,
  useTema,
  type Densidade,
  type PreferenciaTema,
} from "../tema/preferencias";
import { cn } from "../utils/cn";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
} from "./DropdownMenu";
import { SegmentedControl } from "./SegmentedControl";

const OPCOES_TEMA: readonly { valor: PreferenciaTema; rotulo: string }[] = [
  { valor: "sistema", rotulo: "Sistema" },
  { valor: "claro", rotulo: "Claro" },
  { valor: "escuro", rotulo: "Escuro" },
];

const OPCOES_DENSIDADE: readonly { valor: Densidade; rotulo: string }[] = [
  { valor: "confortavel", rotulo: "Confortável" },
  { valor: "compacta", rotulo: "Compacta" },
];

const ROTULO_TEMA: Record<PreferenciaTema, string> = {
  sistema: "do sistema",
  claro: "claro",
  escuro: "escuro",
};

function ehTema(valor: string): valor is PreferenciaTema {
  return OPCOES_TEMA.some((o) => o.valor === valor);
}

function ehDensidade(valor: string): valor is Densidade {
  return OPCOES_DENSIDADE.some((o) => o.valor === valor);
}

interface ThemeToggleProps {
  /** Inclui a escolha de densidade (tabelas e listas) no mesmo menu. */
  comDensidade?: boolean;
  /** Ícone sobre marinho (top bar mobile). */
  sobreMarinho?: boolean;
}

/** Menu de aparência: Sistema/Claro/Escuro em radio (+ densidade). Persiste em localStorage. */
export function ThemeToggle({ comDensidade = false, sobreMarinho = false }: ThemeToggleProps) {
  const { preferencia, resolvido } = useTema();
  const densidade = useDensidade();
  const Icone = preferencia === "sistema" ? Monitor : resolvido === "escuro" ? Moon : Sun;
  return (
    <DropdownMenu>
      <RadixMenu.Trigger
        aria-label={`Aparência: tema ${ROTULO_TEMA[preferencia]}`}
        className={cn(
          "grid size-10 shrink-0 place-items-center rounded-md transition-colors duration-(--dur-fast)",
          sobreMarinho
            ? "text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy focus-visible:outline-gold-line"
            : "text-ink-2 hover:bg-hover-tint hover:text-ink",
        )}
      >
        <Icone size={18} aria-hidden />
      </RadixMenu.Trigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>Tema</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={preferencia}
          onValueChange={(valor) => {
            if (ehTema(valor)) definirPreferenciaTema(valor);
          }}
        >
          {OPCOES_TEMA.map((opcao) => (
            <DropdownMenuRadioItem key={opcao.valor} value={opcao.valor}>
              {opcao.rotulo}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        {comDensidade && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuLabel>Densidade das tabelas</DropdownMenuLabel>
            <DropdownMenuRadioGroup
              value={densidade}
              onValueChange={(valor) => {
                if (ehDensidade(valor)) definirDensidade(valor);
              }}
            >
              {OPCOES_DENSIDADE.map((opcao) => (
                <DropdownMenuRadioItem key={opcao.valor} value={opcao.valor}>
                  {opcao.rotulo}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Versão em controles segmentados, para o rodapé do drawer mobile. */
export function PreferenciasAparencia() {
  const { preferencia } = useTema();
  const densidade = useDensidade();
  const id = useId();
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <span id={`${id}-tema`} className="type-caption text-on-navy-3">
          Tema
        </span>
        <SegmentedControl
          aria-labelledby={`${id}-tema`}
          opcoes={OPCOES_TEMA}
          valor={preferencia}
          aoMudar={definirPreferenciaTema}
          tamanho="lg"
          larguraTotal
          sobreMarinho
        />
      </div>
      <div className="flex flex-col gap-1.5">
        <span id={`${id}-densidade`} className="type-caption text-on-navy-3">
          Densidade das tabelas
        </span>
        <SegmentedControl
          aria-labelledby={`${id}-densidade`}
          opcoes={OPCOES_DENSIDADE}
          valor={densidade}
          aoMudar={definirDensidade}
          tamanho="lg"
          larguraTotal
          sobreMarinho
        />
      </div>
    </div>
  );
}

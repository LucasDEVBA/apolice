import { ArrowLeftRight, ChevronsUpDown, Lock, Menu } from "lucide-react";
import { DropdownMenu as RadixMenu } from "radix-ui";
import { useId, type ReactNode } from "react";
import { NavLink } from "react-router";

import { Avatar } from "@/ui/componentes/Avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
} from "@/ui/componentes/DropdownMenu";
import { notificar } from "@/ui/componentes/notificar";
import { Tooltip } from "@/ui/componentes/Tooltip";
import { Marca } from "@/ui/marca/Marca";
import { cn } from "@/ui/utils/cn";

import {
  FILAS_POR_PAPEL,
  ITEM_ADMIN,
  ITEM_SOBRE,
  NAVEGACAO_PRINCIPAL,
  type FilaSalva,
  type ItemNavegacao,
} from "../navegacao";
import { usePersona } from "../persona-contexto";
import { PERSONAS, type IdPersona } from "../personas";

const PONTO_DO_TOM = {
  ok: "bg-ok-on-navy",
  warn: "bg-warn-on-navy",
  bad: "bg-bad-on-navy",
  info: "bg-info-on-navy",
  neutral: "bg-on-navy-3",
} as const;

// 44 px no drawer do mobile, 40 px a partir do md (DESIGN.md §3.9/§7).
const ITEM_BASE =
  "group flex h-11 items-center gap-3 md:h-10 rounded-md px-3 text-sm font-medium text-on-navy-2 transition-colors duration-(--dur-fast) " +
  "hover:bg-hover-tint-navy hover:text-on-navy aria-[current=page]:bg-sidebar-active aria-[current=page]:font-semibold aria-[current=page]:text-on-navy";

const ICONE_ITEM = "shrink-0 text-on-navy-3 group-aria-[current=page]:text-gold-line";

function Contador({ valor }: { valor: number | undefined }) {
  if (valor === undefined) return null;
  return (
    <span className="ml-auto text-xs font-semibold text-on-navy-2 tnum">
      {/* Separador só para o nome acessível ("Cotações, 57", não "Cotações57"). */}
      <span className="sr-only">, {valor.toLocaleString("pt-BR")}</span>
      <span aria-hidden>{valor.toLocaleString("pt-BR")}</span>
    </span>
  );
}

function ItemNav({
  item,
  aoNavegar,
}: {
  item: ItemNavegacao;
  aoNavegar?: (() => void) | undefined;
}) {
  const Icone = item.icone;
  return (
    <NavLink to={item.para} end={item.exato ?? false} className={ITEM_BASE} onClick={aoNavegar}>
      <Icone size={18} className={ICONE_ITEM} aria-hidden />
      {item.rotulo}
      <Contador valor={item.contador} />
    </NavLink>
  );
}

/** Admin para quem não é Admin: visível com cadeado, sem link nem foco (DESIGN.md §4.2). */
function ItemRestrito({ item }: { item: ItemNavegacao }) {
  const Icone = item.icone;
  return (
    <span className="flex h-11 items-center gap-3 rounded-md px-3 text-sm font-medium text-on-navy-3 md:h-10">
      <Icone size={18} className="shrink-0" aria-hidden />
      {item.rotulo}
      <Lock size={14} className="ml-auto" aria-hidden />
      <span className="sr-only">(restrito ao perfil Admin)</span>
    </span>
  );
}

function SeletorCorretora() {
  return (
    <DropdownMenu>
      <RadixMenu.Trigger className="mx-1.5 mt-[22px] mb-[18px] flex items-center justify-between gap-2 rounded-lg border border-gold-line-soft px-3 py-3 text-left hover:bg-hover-tint-navy">
        <span className="min-w-0">
          <span className="block truncate text-item leading-snug font-semibold text-on-navy">
            Ribeiro &amp; Associados
          </span>
          <span className="block text-xs text-on-navy-3">Corretora de seguros</span>
        </span>
        <ChevronsUpDown size={15} className="shrink-0 text-on-navy-3" aria-hidden />
        <span className="sr-only">(trocar corretora)</span>
      </RadixMenu.Trigger>
      <DropdownMenuContent className="w-60">
        <DropdownMenuLabel>Corretora</DropdownMenuLabel>
        <DropdownMenuRadioGroup value="ribeiro">
          <DropdownMenuRadioItem value="ribeiro">Ribeiro &amp; Associados</DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Menu ⇄ "Trocar persona": lista as 4 personas de demonstração. */
export function MenuPersona({ gatilho }: { gatilho: ReactNode }) {
  const { persona, trocarPersona } = usePersona();
  return (
    <DropdownMenu>
      <RadixMenu.Trigger asChild>{gatilho}</RadixMenu.Trigger>
      <DropdownMenuContent align="end" side="top" className="w-72">
        <DropdownMenuLabel>Entrar como (demonstração)</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={persona.id}
          onValueChange={(valor) => {
            const escolhida = PERSONAS.find((p) => p.id === valor);
            if (!escolhida || escolhida.id === persona.id) return;
            trocarPersona(valor as IdPersona);
            notificar.info({
              titulo: `Agora você é ${escolhida.nome}`,
              descricao: escolhida.escopo,
            });
          }}
        >
          {PERSONAS.map((p) => (
            <DropdownMenuRadioItem key={p.id} value={p.id} className="h-auto py-2">
              <span className="flex flex-col">
                <span className="font-semibold">
                  {p.nome} <span className="font-normal text-ink-2">· {p.rotuloPapel}</span>
                </span>
                <span className="text-xs text-ink-3">{p.escopo}</span>
              </span>
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function RodapeUsuario() {
  const { persona } = usePersona();
  return (
    <div className="mt-auto flex items-center gap-2.5 border-t border-line-navy px-2 pt-3 pb-1">
      <Avatar
        nome={persona.nome}
        variante={persona.avatar === "marinho" ? "marinho" : "navy"}
        anel
      />
      <div className="min-w-0 flex-1">
        <p className="truncate text-item leading-snug font-semibold text-on-navy">{persona.nome}</p>
        <p className="truncate text-xs text-on-navy-3">{persona.rotuloPapel}</p>
      </div>
      <MenuPersona
        gatilho={
          <button
            type="button"
            aria-label="Trocar persona"
            className="grid size-11 shrink-0 place-items-center rounded-md text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy md:size-10"
          >
            <ArrowLeftRight size={15} aria-hidden />
          </button>
        }
      />
    </div>
  );
}

/**
 * Filas salvas: grupo rotulado dentro do nav (não section/h2, que criaria landmark e um título
 * antes do H1 da página). Itens de 44/40 px com ponto no tom, rótulo e contador.
 */
function FilasSalvas({
  filas,
  aoNavegar,
}: {
  filas: readonly FilaSalva[];
  aoNavegar?: (() => void) | undefined;
}) {
  const idFilas = useId();
  return (
    <div role="group" aria-labelledby={idFilas} className="mb-3">
      <p id={idFilas} className="px-3 pb-1.5 text-xs font-semibold text-on-navy-3">
        Filas salvas
      </p>
      <ul className="flex flex-col gap-0.5">
        {filas.map((fila) => (
          <li key={fila.para}>
            <NavLink
              to={fila.para}
              onClick={aoNavegar}
              className="flex h-11 items-center gap-3 rounded-md px-3 text-item text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy md:h-10"
            >
              <span
                aria-hidden
                className={cn("size-1.5 shrink-0 rounded-full", PONTO_DO_TOM[fila.tom])}
              />
              {fila.rotulo}
              <Contador valor={fila.contador} />
            </NavLink>
          </li>
        ))}
      </ul>
    </div>
  );
}

interface ConteudoSidebarProps {
  /** Fecha o drawer ao navegar (mobile). */
  aoNavegar?: () => void;
  rodapeExtra?: ReactNode;
}

/** Anatomia completa: marca → corretora → navegação → filas salvas → sobre → usuário. */
export function ConteudoSidebar({ aoNavegar, rodapeExtra }: ConteudoSidebarProps) {
  const { persona } = usePersona();
  const filas = FILAS_POR_PAPEL[persona.papel];
  const ehAdmin = persona.papel === "Admin";
  return (
    <div className="on-navy flex min-h-full flex-col bg-sidebar px-3.5 pt-[22px] pb-4 text-on-navy-2">
      <Marca className="px-2.5 pt-1 text-on-navy" />
      <SeletorCorretora />
      <nav aria-label="Principal" className="flex flex-col gap-0.5">
        {NAVEGACAO_PRINCIPAL.map((item) => (
          <ItemNav key={item.para} item={item} aoNavegar={aoNavegar} />
        ))}
        {ehAdmin ? (
          <ItemNav item={ITEM_ADMIN} aoNavegar={aoNavegar} />
        ) : (
          <ItemRestrito item={ITEM_ADMIN} />
        )}

        <div aria-hidden className="mx-2.5 my-3.5 h-px bg-line-navy" />

        {filas.length > 0 && <FilasSalvas filas={filas} aoNavegar={aoNavegar} />}

        <ItemNav item={ITEM_SOBRE} aoNavegar={aoNavegar} />
      </nav>
      {rodapeExtra}
      <RodapeUsuario />
    </div>
  );
}

/**
 * Sidebar completa de 248 px (xl+). Região nomeada (section), não aside: complementary é da coluna
 * lateral do detalhe (DESIGN.md §7). A região cobre marca, corretora e usuário, que ficam fora do
 * nav "Principal".
 */
export function SidebarCompleta({ className }: { className?: string }) {
  return (
    <section
      aria-label="Navegação lateral"
      data-shell="sidebar"
      className={cn("sticky top-0 h-dvh w-[248px] overflow-y-auto", className)}
    >
      <ConteudoSidebar />
    </section>
  );
}

const RAIL_ITEM =
  "group grid size-12 place-items-center rounded-md text-on-navy-3 transition-colors duration-(--dur-fast) " +
  "hover:bg-hover-tint-navy hover:text-on-navy aria-[current=page]:bg-sidebar-active aria-[current=page]:text-gold-line";

function ItemRail({ item }: { item: ItemNavegacao }) {
  const Icone = item.icone;
  const dica =
    item.contador === undefined
      ? item.rotulo
      : `${item.rotulo} · ${item.contador.toLocaleString("pt-BR")}`;
  return (
    <Tooltip conteudo={dica} lado="right">
      <NavLink
        to={item.para}
        end={item.exato ?? false}
        aria-label={item.rotulo}
        className={RAIL_ITEM}
      >
        <Icone size={20} aria-hidden />
      </NavLink>
    </Tooltip>
  );
}

/** Rail de 72 px (md–xl): ícones com tooltip; o ☰ abre a sidebar completa em overlay. */
export function SidebarRail({
  aoAbrirMenu,
  className,
}: {
  aoAbrirMenu: () => void;
  className?: string;
}) {
  const { persona } = usePersona();
  const ehAdmin = persona.papel === "Admin";
  return (
    <section
      aria-label="Navegação lateral"
      data-shell="rail"
      className={cn(
        "on-navy sticky top-0 h-dvh w-[72px] flex-col items-center gap-1 bg-sidebar py-3 text-on-navy-2",
        className,
      )}
    >
      <button
        type="button"
        onClick={aoAbrirMenu}
        aria-label="Abrir menu completo"
        className="grid size-12 place-items-center rounded-md text-on-navy-2 hover:bg-hover-tint-navy hover:text-on-navy"
      >
        <Menu size={20} aria-hidden />
      </button>
      <nav aria-label="Principal" className="mt-2 flex flex-col items-center gap-1">
        {NAVEGACAO_PRINCIPAL.map((item) => (
          <ItemRail key={item.para} item={item} />
        ))}
        {ehAdmin ? (
          <ItemRail item={ITEM_ADMIN} />
        ) : (
          <span className="grid size-12 place-items-center text-on-navy-3/70">
            <Lock size={18} aria-hidden />
            <span className="sr-only">Admin (restrito ao perfil Admin)</span>
          </span>
        )}
        <div aria-hidden className="my-2 h-px w-8 bg-line-navy" />
        <ItemRail item={ITEM_SOBRE} />
      </nav>
      <div className="mt-auto">
        <MenuPersona
          gatilho={
            <button type="button" className="grid size-12 place-items-center rounded-full">
              <Avatar
                nome={persona.nome}
                variante={persona.avatar === "marinho" ? "marinho" : "navy"}
                anel
              />
              <span className="sr-only">
                Trocar persona (atual: {persona.nome}, {persona.rotuloPapel})
              </span>
            </button>
          }
        />
      </div>
    </section>
  );
}

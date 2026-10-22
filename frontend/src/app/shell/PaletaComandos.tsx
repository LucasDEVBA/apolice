import {
  Calculator,
  CornerDownLeft,
  LifeBuoy,
  Plus,
  Search,
  ShieldCheck,
  type LucideIcon,
} from "lucide-react";
import { useEffect, useId, useState, type KeyboardEvent } from "react";
import { useNavigate } from "react-router";

import { Dialog } from "@/ui/componentes/Dialog";
import { cn } from "@/ui/utils/cn";

import { ITEM_ADMIN, ITEM_SOBRE, NAVEGACAO_PRINCIPAL } from "../navegacao";
import { usePersona } from "../persona-contexto";
import { pode } from "../sessao/permissoes";

interface Comando {
  id: string;
  rotulo: string;
  dica: string;
  para: string;
  icone: LucideIcon;
}

function normalizar(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

function useComandos(): Comando[] {
  const { persona } = usePersona();
  const navegacao = [
    ...NAVEGACAO_PRINCIPAL,
    ...(persona.papel === "Admin" ? [ITEM_ADMIN] : []),
    ITEM_SOBRE,
  ];
  const acoes: Comando[] = pode(persona.papel, "operarCarteira")
    ? [
        {
          id: "nova-cotacao",
          rotulo: "Nova cotação",
          dica: "Ação",
          para: "/cotacoes/nova",
          icone: Plus,
        },
      ]
    : [];
  return [
    ...acoes,
    ...navegacao.map((item) => ({
      id: item.para,
      rotulo: item.rotulo,
      dica: "Ir para",
      para: item.para,
      icone: item.icone,
    })),
  ];
}

const ATRASO_ANUNCIO_MS = 400;

/** Busca de registros: leva o termo para a busca da lista (a API filtra por número, nome e CPF). */
function comandosDeBusca(termo: string): Comando[] {
  const limpo = termo.trim();
  if (limpo === "") return [];
  const busca = encodeURIComponent(limpo);
  return [
    {
      id: "busca-apolices",
      rotulo: `Buscar “${limpo}” em apólices`,
      dica: "Busca",
      para: `/apolices?busca=${busca}`,
      icone: ShieldCheck,
    },
    {
      id: "busca-cotacoes",
      rotulo: `Buscar “${limpo}” em cotações`,
      dica: "Busca",
      para: `/cotacoes?busca=${busca}`,
      icone: Calculator,
    },
    {
      id: "busca-sinistros",
      rotulo: `Buscar “${limpo}” em sinistros`,
      dica: "Busca",
      para: `/sinistros?busca=${busca}`,
      icone: LifeBuoy,
    },
  ];
}

function textoDoResultado(termo: string, total: number): string {
  if (termo.trim() === "") return "";
  if (total === 0) return `Nenhum comando para “${termo.trim()}”.`;
  return total === 1 ? "1 resultado." : `${String(total)} resultados.`;
}

/**
 * Contagem de resultados para leitor de tela, numa região que já existe desde a abertura (uma
 * região criada junto com o texto não é lida) e com atraso, para não falar a cada tecla.
 */
function useAnuncioDeResultados(termo: string, total: number): string {
  const [anuncio, setAnuncio] = useState("");
  useEffect(() => {
    const timer = window.setTimeout(
      () => setAnuncio(textoDoResultado(termo, total)),
      ATRASO_ANUNCIO_MS,
    );
    return () => window.clearTimeout(timer);
  }, [termo, total]);
  return anuncio;
}

interface PaletaComandosProps {
  aberta: boolean;
  aoMudar: (aberta: boolean) => void;
}

/**
 * Paleta de comandos (⌘K / Ctrl K): Dialog + combobox com lista filtrável, setas e Enter
 * (padrão ARIA combobox com aria-activedescendant). A busca de registros entra na fase 2.
 */
export function PaletaComandos({ aberta, aoMudar }: PaletaComandosProps) {
  const [termo, setTermo] = useState("");
  const [ativo, setAtivo] = useState(0);
  const navegar = useNavigate();
  const idLista = useId();
  const comandos = useComandos();
  const filtrados = [
    ...comandos.filter((c) => normalizar(c.rotulo).includes(normalizar(termo.trim()))),
    ...comandosDeBusca(termo),
  ];
  const indiceAtivo = Math.min(ativo, Math.max(0, filtrados.length - 1));
  const idOpcao = (i: number) => `${idLista}-opcao-${String(i)}`;
  const vazia = filtrados.length === 0;
  const anuncio = useAnuncioDeResultados(termo, filtrados.length);

  function mudar(novo: boolean) {
    if (!novo) {
      setTermo("");
      setAtivo(0);
    }
    aoMudar(novo);
  }

  function executar(comando: Comando | undefined) {
    if (!comando) return;
    mudar(false);
    void navegar(comando.para);
  }

  function aoTeclar(evento: KeyboardEvent<HTMLInputElement>) {
    const total = filtrados.length;
    if (evento.key === "ArrowDown" && total > 0) {
      evento.preventDefault();
      setAtivo((indiceAtivo + 1) % total);
    } else if (evento.key === "ArrowUp" && total > 0) {
      evento.preventDefault();
      setAtivo((indiceAtivo - 1 + total) % total);
    } else if (evento.key === "Enter") {
      evento.preventDefault();
      executar(filtrados[indiceAtivo]);
    }
  }

  return (
    <Dialog aberto={aberta} aoMudar={mudar} titulo="Buscar e navegar" largura="formulario">
      <div className="flex h-11 items-center gap-2.5 rounded-md border focus-campo border-control bg-surface px-3.5">
        <Search size={16} className="shrink-0 text-ink-3" aria-hidden />
        <input
          role="combobox"
          aria-expanded={!vazia}
          aria-controls={idLista}
          aria-autocomplete="list"
          aria-activedescendant={vazia ? undefined : idOpcao(indiceAtivo)}
          aria-label="Buscar comando ou página"
          placeholder="Página, ação, apólice, segurado ou CPF"
          value={termo}
          onChange={(e) => {
            setTermo(e.target.value);
            setAtivo(0);
          }}
          onKeyDown={aoTeclar}
          className="h-full min-w-0 flex-1 bg-transparent text-base text-ink outline-none placeholder:text-ink-3 md:text-campo"
        />
      </div>
      {/* Lista vazia fica oculta (listbox sem opções é inválido); o aviso visível vem abaixo. */}
      <div
        id={idLista}
        role="listbox"
        aria-label="Resultados"
        hidden={vazia}
        className="mt-3 flex flex-col gap-0.5"
      >
        {filtrados.map((comando, i) => {
          const Icone = comando.icone;
          return (
            // O teclado fica no combobox (aria-activedescendant); a opção só recebe clique e hover.
            <div
              key={comando.id}
              id={idOpcao(i)}
              role="option"
              tabIndex={-1}
              aria-selected={i === indiceAtivo}
              onClick={() => executar(comando)}
              onKeyDown={(e) => {
                if (e.key === "Enter") executar(comando);
              }}
              onMouseMove={() => setAtivo(i)}
              className={cn(
                "flex h-11 cursor-pointer items-center gap-3 rounded-md px-3 text-sm text-ink",
                i === indiceAtivo && "bg-surface-selected",
              )}
            >
              <Icone size={16} className="shrink-0 text-ink-2" aria-hidden />
              <span className="flex-1">{comando.rotulo}</span>
              <span className="type-meta text-ink-3">{comando.dica}</span>
              {i === indiceAtivo && <CornerDownLeft size={13} className="text-ink-3" aria-hidden />}
            </div>
          );
        })}
      </div>
      {vazia && (
        <p aria-hidden className="px-3 py-6 text-center text-sm text-ink-3">
          Nenhum comando para “{termo}”.
        </p>
      )}
      <p role="status" className="sr-only">
        {anuncio}
      </p>
      <p className="mt-4 type-meta text-ink-3">
        Digite um número de apólice, nome do segurado ou CPF para buscar nas listas.
      </p>
    </Dialog>
  );
}

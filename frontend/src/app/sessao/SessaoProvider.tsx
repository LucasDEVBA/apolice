import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";

import type { AuthResponse, UsuarioDto } from "@/api/contratos";
import * as auth from "@/api/endpoints/auth";
import {
  aoExpirarSessao,
  aoRenovarSessao,
  avisarFimDaSessao,
  definirTokenDeAcesso,
  renovarSessao,
} from "@/api/http";
import { descartarTodosOsRascunhos } from "@/paginas/cotacoes/nova/rascunho";
import { notificar } from "@/ui/componentes/notificar";

import type { IdPersona } from "../personas";
import { SessaoContexto, type EstadoSessao, type ValorSessao } from "./sessao-contexto";

/** Id fixo: várias chamadas recusadas juntas mostram UM aviso, não um por requisição. */
const TOAST_SESSAO_EXPIRADA = "sessao-expirada";
const TOAST_SESSAO_TROCADA = "sessao-trocada";

function registrar(acao: string, dados: Record<string, unknown> = {}) {
  // Log estruturado sem dado sensível: nunca token, e-mail ou senha.
  console.info({ nivel: "info", acao, ...dados });
}

/**
 * Sessão do app: access token só em memória (http.ts), restauração no load pelo cookie HttpOnly de
 * refresh, renovação transparente em 401 e fim da sessão quando o refresh é recusado.
 */
export function SessaoProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [estado, setEstado] = useState<EstadoSessao>({ status: "verificando" });

  // Restaura a sessão na carga: sem cookie válido, a API responde 401 e o app vai para o login.
  useEffect(() => {
    let ativo = true;
    renovarSessao()
      .then((resposta) => {
        if (ativo) setEstado({ status: "autenticada", usuario: resposta.usuario });
      })
      .catch(() => {
        if (ativo) setEstado({ status: "anonima" });
      });
    return () => {
      ativo = false;
    };
  }, []);

  // O ouvinte de renovação é registrado uma vez e precisa do estado atual, não o da montagem.
  const estadoAtual = useRef(estado);
  useEffect(() => {
    estadoAtual.current = estado;
  }, [estado]);

  useEffect(() => {
    const pararRenovacao = aoRenovarSessao((resposta) => {
      const atual = estadoAtual.current;
      if (atual.status !== "autenticada") return;
      if (atual.usuario.id === resposta.usuario.id) {
        setEstado({ status: "autenticada", usuario: resposta.usuario });
        return;
      }
      // O cookie de refresh é do navegador, não da aba: se outra aba entrou com outra pessoa, a
      // renovação desta devolve esse usuário. Vira sessão nova (cache, rascunho e permissões).
      queryClient.clear();
      descartarTodosOsRascunhos();
      setEstado({ status: "autenticada", usuario: resposta.usuario });
      registrar("sessao_trocada_por_outra_aba", { papel: resposta.usuario.papel });
      notificar.alerta(
        {
          titulo: `A sessão desta aba mudou para ${resposta.usuario.nome}`,
          descricao: "Outra aba entrou com outro usuário. O que você vê agora é dessa sessão.",
        },
        { id: TOAST_SESSAO_TROCADA },
      );
    });
    const pararExpiracao = aoExpirarSessao(() => {
      queryClient.clear();
      descartarTodosOsRascunhos();
      setEstado({ status: "anonima", motivo: "expirada" });
      notificar.alerta(
        { titulo: "Sua sessão expirou", descricao: "Entre novamente para continuar." },
        { id: TOAST_SESSAO_EXPIRADA },
      );
    });
    return () => {
      pararRenovacao();
      pararExpiracao();
    };
  }, [queryClient]);

  const valor = useMemo<ValorSessao>(() => {
    function iniciar(resposta: AuthResponse): UsuarioDto {
      definirTokenDeAcesso(resposta.accessToken);
      avisarFimDaSessao();
      // Outro usuário, outro escopo: nada do cache nem do rascunho anterior pode aparecer para ele.
      queryClient.clear();
      descartarTodosOsRascunhos();
      setEstado({ status: "autenticada", usuario: resposta.usuario });
      registrar("sessao_iniciada", { papel: resposta.usuario.papel });
      return resposta.usuario;
    }

    async function encerrarNoServidor() {
      try {
        await auth.logout();
      } catch (erro) {
        // Logout é best-effort: o token some da memória de qualquer jeito; o refresh expira sozinho.
        console.warn({ nivel: "aviso", acao: "logout_falhou", erro });
      }
    }

    return {
      estado,
      entrar: async (email, senha) => iniciar(await auth.login({ email, senha })),
      entrarComoPersona: async (persona) => iniciar(await auth.loginDemo({ persona })),
      sair: async () => {
        await encerrarNoServidor();
        definirTokenDeAcesso(null);
        avisarFimDaSessao();
        queryClient.clear();
        descartarTodosOsRascunhos();
        setEstado({ status: "anonima", motivo: "saiu" });
        registrar("sessao_encerrada");
      },
      trocarPersona: async (persona: IdPersona) => {
        await encerrarNoServidor();
        descartarTodosOsRascunhos();
        return iniciar(await auth.loginDemo({ persona }));
      },
    };
  }, [estado, queryClient]);

  return <SessaoContexto value={valor}>{children}</SessaoContexto>;
}

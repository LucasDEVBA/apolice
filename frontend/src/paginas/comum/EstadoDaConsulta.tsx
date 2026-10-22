import { FileSearch } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { ErroApi, ehNaoEncontrado, mensagemDoErro } from "@/api/erros";
import { Button } from "@/ui/componentes/Button";
import { Card } from "@/ui/componentes/Card";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { ErrorState } from "@/ui/componentes/ErrorState";

interface EstadoDaConsultaProps {
  erro: unknown;
  /** "a apólice", "o sinistro"… */
  entidade: string;
  voltarPara: { rotulo: string; href: string };
  aoTentarDeNovo: () => void;
  nivel?: "h1" | "h2";
}

/**
 * Erro de uma consulta de detalhe. 404 é o mesmo para "não existe" e "é de outro corretor"
 * (anti-BOLA, SPEC §5): a tela diz "não encontrado" sem insinuar que o registro existe.
 */
export function EstadoDaConsulta({
  erro,
  entidade,
  voltarPara,
  aoTentarDeNovo,
  nivel = "h2",
}: EstadoDaConsultaProps) {
  if (ehNaoEncontrado(erro)) {
    return (
      <Card>
        <EmptyState
          icone={FileSearch}
          nivel={nivel}
          titulo={`Não encontramos ${entidade}`}
          descricao="O endereço pode estar errado, ou o registro não faz parte da sua carteira."
          acao={
            <Button asChild>
              <Link to={voltarPara.href}>{voltarPara.rotulo}</Link>
            </Button>
          }
        />
      </Card>
    );
  }
  const { descricao } = mensagemDoErro(erro);
  return (
    <Card>
      <ErrorState
        titulo={`Não foi possível carregar ${entidade}`}
        nivel={nivel}
        {...(descricao ? { descricao } : {})}
        correlationId={erro instanceof ErroApi ? erro.correlationId : undefined}
        aoTentarDeNovo={aoTentarDeNovo}
      />
    </Card>
  );
}

/** Erro de lista dentro do card da tabela (sem mudar o título da página). */
export function ErroDeLista({
  erro,
  oQue,
  aoTentarDeNovo,
}: {
  erro: unknown;
  oQue: string;
  aoTentarDeNovo: () => void;
}): ReactNode {
  const { descricao } = mensagemDoErro(erro);
  return (
    <ErrorState
      titulo={`Não foi possível carregar ${oQue}`}
      {...(descricao ? { descricao } : {})}
      correlationId={erro instanceof ErroApi ? erro.correlationId : undefined}
      aoTentarDeNovo={aoTentarDeNovo}
    />
  );
}

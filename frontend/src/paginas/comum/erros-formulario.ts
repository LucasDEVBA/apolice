import type { FieldValues, Path, UseFormSetError } from "react-hook-form";

import { ErroApi, mensagemDoErro } from "@/api/erros";
import { notificar } from "@/ui/componentes/notificar";

/**
 * Erro de mutação com formulário (meta.errosNoFormulario): os erros por campo do ProblemDetails
 * vão para os campos (`mapa` traduz "segurado.cpf" → nome do campo); se nenhum casar, o erro vira
 * toast com o correlationId, como os demais. Nunca some em silêncio.
 */
export function tratarErroNoFormulario<T extends FieldValues>(
  erro: unknown,
  setError: UseFormSetError<T>,
  mapa: Readonly<Record<string, Path<T>>>,
): void {
  if (!(erro instanceof ErroApi) || !erro.temErrosPorCampo) return;
  let aplicou = false;
  for (const [caminho, mensagens] of Object.entries(erro.errosPorCampo)) {
    const campo = mapa[caminho];
    const mensagem = mensagens[0];
    if (!campo || !mensagem) continue;
    setError(campo, { type: "api", message: mensagem }, { shouldFocus: !aplicou });
    aplicou = true;
  }
  if (aplicou) return;
  const { titulo, descricao } = mensagemDoErro(erro);
  notificar.erro({
    titulo,
    ...(descricao ? { descricao } : {}),
    ...(erro.correlationId ? { correlationId: erro.correlationId } : {}),
  });
}

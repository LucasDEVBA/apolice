import { zodResolver } from "@hookform/resolvers/zod";
import { Pencil, Save } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { z } from "zod";

import { useAtualizarTaxa, useProdutos } from "@/api/consultas/produtos";
import type { CoberturaProdutoDto, ProdutoDto } from "@/api/contratos";
import { Button } from "@/ui/componentes/Button";
import { Card, CardHeader } from "@/ui/componentes/Card";
import { Dialog } from "@/ui/componentes/Dialog";
import { FormField } from "@/ui/componentes/FormField";
import { Input } from "@/ui/componentes/Input";
import { notificar } from "@/ui/componentes/notificar";
import { PRODUTOS } from "@/ui/componentes/produtos";
import { SkeletonTexto } from "@/ui/componentes/Skeleton";
import { cn } from "@/ui/utils/cn";
import {
  formatarMoeda,
  formatarNumero,
  formatarPercentual,
  lerMoedaDigitada,
} from "@/ui/utils/formatar";

import { ErroDeLista } from "../comum/EstadoDaConsulta";
import { tratarErroNoFormulario } from "../comum/erros-formulario";
import { RodapeDoDialogo } from "../comum/RodapeDoDialogo";
import { ROTULO_TIPO_CALCULO } from "../comum/rotulos";
import { AreaAdmin } from "./AreaAdmin";

const CASAS_TAXA = 6;
const POR_MIL = 1000;

/** Taxa como o negócio lê: % do LMI (taxa sobre LMI) ou ‰ (por mil). A API guarda a fração. */
function taxaLegivel(c: CoberturaProdutoDto): string {
  if (c.tipoCalculo === "PremioFixo") return `${formatarMoeda(c.premioFixo ?? 0)} fixo`;
  if (c.tipoCalculo === "TaxaPorMil") return `${formatarNumero(c.taxa, 2)} ‰`;
  return formatarPercentual(c.taxa, 3);
}

function faixaLmi(c: CoberturaProdutoDto): string {
  if (c.lmiFixo !== null) return c.lmiFixo === 0 ? "sem LMI" : `fixo ${formatarMoeda(c.lmiFixo)}`;
  return `${formatarMoeda(c.lmiMinimo)} a ${formatarMoeda(c.lmiMaximo)}`;
}

/**
 * A API aceita taxa > 0 e ≤ 1 nos dois tipos (CoberturaProduto.TaxaValida), mas a unidade muda:
 * fração do LMI na taxa sobre o LMI e número em ‰ na taxa por mil (SPEC §4.2).
 */
const UNIDADE_DA_TAXA = {
  TaxaSobreLmi: {
    rotulo: "Nova taxa (fração do LMI)",
    erro: "A taxa é uma fração maior que zero e de no máximo 1 (ex.: 0,032 = 3,2% do LMI).",
  },
  TaxaPorMil: {
    rotulo: "Nova taxa (‰ do LMI)",
    erro: "A taxa por mil é maior que zero e de no máximo 1 (ex.: 0,8 = 0,8 ‰ do LMI).",
  },
} as const;

type TipoComTaxa = keyof typeof UNIDADE_DA_TAXA;

function criarEsquema(tipo: TipoComTaxa) {
  return z.object({
    taxa: z
      .string()
      .trim()
      .min(1, "Informe a nova taxa.")
      .refine((t) => {
        const valor = lerMoedaDigitada(t);
        return valor !== undefined && !Number.isNaN(valor) && valor > 0 && valor <= 1;
      }, UNIDADE_DA_TAXA[tipo].erro),
  });
}
type EdicaoTaxa = z.infer<ReturnType<typeof criarEsquema>>;

function FormularioTaxa({
  produto,
  cobertura,
  aoFechar,
}: {
  produto: ProdutoDto;
  cobertura: CoberturaProdutoDto;
  aoFechar: () => void;
}) {
  const atualizar = useAtualizarTaxa();
  const tipo: TipoComTaxa = cobertura.tipoCalculo === "TaxaPorMil" ? "TaxaPorMil" : "TaxaSobreLmi";
  const {
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<EdicaoTaxa>({
    resolver: zodResolver(criarEsquema(tipo)),
    defaultValues: {
      taxa: formatarNumero(cobertura.taxa, CASAS_TAXA).replace(/0+$/, "").replace(/,$/, ""),
    },
  });
  const texto = useWatch({ control, name: "taxa" });
  const nova = lerMoedaDigitada(texto);
  const previa =
    nova !== undefined && !Number.isNaN(nova) && nova > 0 && nova <= 1
      ? cobertura.tipoCalculo === "TaxaPorMil"
        ? `${formatarNumero(nova, 2)} ‰ do LMI`
        : `${formatarPercentual(nova, 3)} do LMI`
      : null;
  // O valor guardado é fração para TaxaSobreLmi e o número em ‰ para TaxaPorMil (SPEC §4.2).
  const exemplo =
    previa && nova !== undefined
      ? (cobertura.lmiMinimo * nova) / (cobertura.tipoCalculo === "TaxaPorMil" ? POR_MIL : 1)
      : null;

  async function salvar(dados: EdicaoTaxa) {
    const taxa = lerMoedaDigitada(dados.taxa);
    if (taxa === undefined) return;
    try {
      await atualizar.mutateAsync({ produto: produto.codigo, cobertura: cobertura.codigo, taxa });
      notificar.sucesso({
        titulo: "Taxa atualizada",
        descricao: `${cobertura.nome}: as próximas cotações já usam a nova taxa.`,
      });
      aoFechar();
    } catch (erro) {
      tratarErroNoFormulario(erro, setError, { taxa: "taxa" });
    }
  }

  return (
    <form noValidate onSubmit={(e) => void handleSubmit(salvar)(e)} className="flex flex-col gap-4">
      <FormField
        rotulo={UNIDADE_DA_TAXA[tipo].rotulo}
        obrigatorio
        erro={errors.taxa?.message}
        ajuda={
          previa
            ? `Equivale a ${previa}${exemplo !== null ? ` · ${formatarMoeda(exemplo)} sobre o LMI mínimo, antes dos fatores` : ""}.`
            : `Atual: ${taxaLegivel(cobertura)}.`
        }
      >
        {(campo) => (
          <Controller
            control={control}
            name="taxa"
            render={({ field }) => (
              <Input
                {...campo}
                {...field}
                inputMode="decimal"
                autoComplete="off"
                onChange={(e) => field.onChange(e.target.value.replace(/[^\d.,]/g, ""))}
              />
            )}
          />
        )}
      </FormField>
      <p className="type-meta text-ink-3">
        A alteração vale para novas cotações e endossos; cotações já calculadas mantêm o preço.
      </p>
      <RodapeDoDialogo>
        <Button variante="secundario" onClick={aoFechar} desabilitado={atualizar.isPending}>
          Voltar
        </Button>
        <Button
          type="submit"
          icone={Save}
          carregando={atualizar.isPending}
          rotuloCarregando="Salvando…"
        >
          Salvar taxa
        </Button>
      </RodapeDoDialogo>
    </form>
  );
}

function TabelaProduto({
  produto,
  aoEditar,
}: {
  produto: ProdutoDto;
  aoEditar: (c: CoberturaProdutoDto) => void;
}) {
  const { icone: Icone, chip } = PRODUTOS[produto.codigo];
  return (
    <Card>
      <CardHeader
        titulo={
          <span className="inline-flex items-center gap-2.5">
            <span aria-hidden className={cn("grid size-8 place-items-center rounded-lg", chip)}>
              <Icone size={16} />
            </span>
            {produto.nome}
          </span>
        }
        subtitulo={`${produto.descricao} IOF ${formatarPercentual(produto.aliquotaIof)}.`}
      />
      <div className="relative mt-4 overflow-x-auto">
        <table className="w-full min-w-[680px] border-collapse text-(length:--fs-table) tnum">
          <caption className="sr-only">Coberturas de {produto.nome}</caption>
          <thead>
            <tr className="text-meta text-ink-2">
              {["Código", "Cobertura", "Cálculo", "Faixa de LMI", "Taxa", ""].map((c, i) => (
                <th
                  key={c || "acoes"}
                  scope="col"
                  className={cn(
                    "border-y border-line bg-surface-2 px-4 py-2.5 font-semibold first:pl-6 last:pr-6",
                    i === 4 ? "text-right" : "text-left",
                  )}
                >
                  {c || <span className="sr-only">Ações</span>}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {produto.coberturas.map((c) => (
              <tr key={c.codigo} className="hover:bg-surface-hover">
                <td className="border-b border-line-soft py-3 pr-4 pl-6 mono-id font-semibold text-ink-2">
                  {c.codigo}
                </td>
                <td className="border-b border-line-soft px-4 py-3 text-ink">
                  {c.nome}
                  <span className="block type-meta text-ink-3">
                    {c.obrigatoria ? "Obrigatória" : "Opcional"}
                    {!c.permiteSinistro && " · sem sinistro"}
                  </span>
                </td>
                <td className="border-b border-line-soft px-4 py-3 text-ink-2">
                  {ROTULO_TIPO_CALCULO[c.tipoCalculo]}
                </td>
                <td className="border-b border-line-soft px-4 py-3 text-ink-2">{faixaLmi(c)}</td>
                <td className="border-b border-line-soft px-4 py-3 text-right font-semibold text-ink">
                  {taxaLegivel(c)}
                </td>
                <td className="border-b border-line-soft py-2 pr-6 pl-4 text-right">
                  {c.tipoCalculo === "PremioFixo" ? (
                    <span className="type-meta text-ink-3">não editável</span>
                  ) : (
                    <Button
                      variante="fantasma"
                      tamanho="sm"
                      icone={Pencil}
                      onClick={() => aoEditar(c)}
                      aria-label={`Editar taxa de ${c.nome}`}
                    >
                      Editar
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

export default function AdminTarifas() {
  const produtos = useProdutos();
  const [edicao, setEdicao] = useState<{
    produto: ProdutoDto;
    cobertura: CoberturaProdutoDto;
  } | null>(null);
  return (
    <AreaAdmin
      titulo="Tarifas"
      subtitulo="Taxas por cobertura dos produtos Auto, Residencial e Vida"
    >
      {produtos.isPending && (
        <Card className="p-6">
          <SkeletonTexto linhas={6} />
        </Card>
      )}
      {produtos.isError && (
        <Card>
          <ErroDeLista
            erro={produtos.error}
            oQue="o catálogo de produtos"
            aoTentarDeNovo={() => void produtos.refetch()}
          />
        </Card>
      )}
      {produtos.data?.map((produto) => (
        <TabelaProduto
          key={produto.codigo}
          produto={produto}
          aoEditar={(cobertura) => setEdicao({ produto, cobertura })}
        />
      ))}
      <Dialog
        aberto={edicao !== null}
        aoMudar={(aberto) => !aberto && setEdicao(null)}
        titulo={edicao ? `Editar taxa · ${edicao.cobertura.nome}` : "Editar taxa"}
        descricao={
          edicao
            ? `${edicao.produto.nome} · ${ROTULO_TIPO_CALCULO[edicao.cobertura.tipoCalculo]}. Prêmio fixo não aceita taxa.`
            : undefined
        }
        largura="confirmacao"
      >
        {edicao && (
          <FormularioTaxa
            produto={edicao.produto}
            cobertura={edicao.cobertura}
            aoFechar={() => setEdicao(null)}
          />
        )}
      </Dialog>
    </AreaAdmin>
  );
}

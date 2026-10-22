import { Pencil } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";
import { useWatch } from "react-hook-form";

import type { ProdutoDto, TarifacaoDto } from "@/api/contratos";
import { ProdutoTag } from "@/ui/componentes/ProdutoTag";
import { Money } from "@/ui/componentes/Valores";
import { hojeEmBrasilia, idadeEm } from "@/ui/utils/datas";
import { formatarCep } from "@/ui/utils/documentos";
import { formatarData, formatarMoeda, mascararCpf } from "@/ui/utils/formatar";

import { ListaCoberturasCompacta } from "../../comum/ListaCoberturasCompacta";

import type { FormularioCotacao } from "./modelo";

function Bloco({
  titulo,
  editar,
  children,
}: {
  titulo: string;
  editar: string;
  children: ReactNode;
}) {
  return (
    <section className="relative rounded-lg border border-line">
      <header className="flex items-center justify-between gap-3 border-b border-line-soft px-4 py-2.5">
        <h3 className="text-sm font-semibold text-ink">{titulo}</h3>
        <Link
          to={editar}
          className="inline-flex h-10 items-center gap-1.5 rounded-md px-2 text-label font-semibold text-link hover:underline"
        >
          <Pencil size={13} aria-hidden />
          Editar<span className="sr-only"> {titulo.toLowerCase()}</span>
        </Link>
      </header>
      <dl className="grid gap-x-6 gap-y-2.5 px-4 py-3.5 text-sm sm:grid-cols-2">{children}</dl>
    </section>
  );
}

function Item({ rotulo, children }: { rotulo: string; children: ReactNode }) {
  return (
    <div>
      <dt className="type-meta text-ink-3">{rotulo}</dt>
      <dd className="break-words text-ink tnum">{children}</dd>
    </div>
  );
}

function ResumoRisco({ v }: { v: FormularioCotacao }) {
  if (v.produto === "AUTO") {
    return (
      <>
        <Item rotulo="Valor do veículo">{formatarMoeda(v.auto.valorVeiculo ?? 0)}</Item>
        <Item rotulo="Ano de fabricação">{v.auto.anoFabricacao}</Item>
        <Item rotulo="CEP de pernoite">{formatarCep(v.auto.cepPernoite)}</Item>
        <Item rotulo="Condutor principal">{v.auto.idadeCondutor} anos</Item>
        <Item rotulo="Uso">{v.auto.usoComercial ? "Comercial" : "Particular"}</Item>
        <Item rotulo="Classe de bônus">{v.auto.classeBonus}</Item>
      </>
    );
  }
  if (v.produto === "RESID") {
    return (
      <>
        <Item rotulo="Valor do imóvel">{formatarMoeda(v.resid.valorImovel ?? 0)}</Item>
        <Item rotulo="Tipo">{v.resid.tipoImovel}</Item>
        <Item rotulo="CEP">{formatarCep(v.resid.cep)}</Item>
        <Item rotulo="Ocupação">{v.resid.habitual ? "Habitual" : "Veraneio"}</Item>
      </>
    );
  }
  return <Item rotulo="Hábito de fumar">{v.vida.fumante ? "Fumante" : "Não fumante"}</Item>;
}

/** Etapa 5: conferência antes de criar a cotação, com links para editar cada etapa. */
export function EtapaRevisao({
  produto,
  tarifacao,
}: {
  produto: ProdutoDto;
  tarifacao: TarifacaoDto | undefined;
}) {
  const v = useWatch<FormularioCotacao>() as FormularioCotacao;
  const idade = /^\d{4}-\d{2}-\d{2}$/.test(v.segurado.dataNascimento)
    ? idadeEm(v.segurado.dataNascimento, hojeEmBrasilia())
    : null;
  return (
    <div className="flex flex-col gap-4">
      <Bloco titulo="Produto" editar="?etapa=1">
        <Item rotulo="Seguro">
          <ProdutoTag produto={produto.codigo} />
        </Item>
      </Bloco>
      <Bloco titulo="Segurado" editar="?etapa=2">
        <Item rotulo="Nome">{v.segurado.nome}</Item>
        <Item rotulo="CPF">{mascararCpf(v.segurado.cpf)}</Item>
        <Item rotulo="E-mail">{v.segurado.email}</Item>
        <Item rotulo="Nascimento">
          {v.segurado.dataNascimento && formatarData(v.segurado.dataNascimento)}
          {idade !== null && ` · ${String(idade)} anos`}
        </Item>
      </Bloco>
      <Bloco titulo="Risco" editar="?etapa=3">
        <ResumoRisco v={v} />
      </Bloco>
      <section className="relative rounded-lg border border-line">
        <header className="flex items-center justify-between gap-3 border-b border-line-soft px-4 py-2.5">
          <h3 className="text-sm font-semibold text-ink">Coberturas</h3>
          <Link
            to="?etapa=4"
            className="inline-flex h-10 items-center gap-1.5 rounded-md px-2 text-label font-semibold text-link hover:underline"
          >
            <Pencil size={13} aria-hidden />
            Editar<span className="sr-only"> coberturas</span>
          </Link>
        </header>
        {tarifacao ? (
          <>
            <div className="hidden sm:block">
              <table className="w-full text-sm">
                <caption className="sr-only">Coberturas da cotação</caption>
                <thead>
                  <tr className="text-left text-meta text-ink-2">
                    <th scope="col" className="px-4 py-2 font-semibold">
                      Cobertura
                    </th>
                    <th scope="col" className="px-4 py-2 text-right font-semibold">
                      LMI
                    </th>
                    <th scope="col" className="px-4 py-2 text-right font-semibold">
                      Prêmio
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {tarifacao.itens.map((item) => (
                    <tr key={item.codigo} className="border-t border-line-soft">
                      <td className="px-4 py-2.5 text-ink">
                        {item.nome}
                        <span className="block mono-id text-ink-3">{item.codigo}</span>
                      </td>
                      <td className="px-4 py-2.5 text-right text-ink-2 tnum">
                        {item.lmi > 0 ? formatarMoeda(item.lmi) : "—"}
                      </td>
                      <td className="px-4 py-2.5 text-right text-ink tnum">
                        {formatarMoeda(item.premioLiquido)}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-line">
                    <th
                      scope="row"
                      colSpan={2}
                      className="px-4 py-3 text-left font-semibold text-ink-2"
                    >
                      Prêmio total (com IOF)
                    </th>
                    <td className="px-4 py-3 text-right">
                      <Money valor={tarifacao.premioTotal} className="font-semibold text-ink" />
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
            <ListaCoberturasCompacta
              rotulo="Coberturas da cotação"
              linhas={tarifacao.itens.map((i) => ({
                codigo: i.codigo,
                nome: i.nome,
                lmi: i.lmi,
                premio: i.premioLiquido,
              }))}
              totais={[
                {
                  rotulo: "Prêmio total (com IOF)",
                  valor: formatarMoeda(tarifacao.premioTotal),
                  forte: true,
                },
              ]}
            />
          </>
        ) : (
          <p className="px-4 py-3.5 text-sm text-ink-3">Aguardando o cálculo…</p>
        )}
      </section>
    </div>
  );
}

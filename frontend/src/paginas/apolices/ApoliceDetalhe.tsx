import { useRef, useState } from "react";
import { useParams } from "react-router";

import { aguardandoBoasVindas, useApolice, useBaixarDocumento } from "@/api/consultas/apolices";
import { useFeatures } from "@/api/consultas/sistema";
import type { ApoliceDto } from "@/api/contratos";
import { useAnuncioDeRota } from "@/app/anuncio-de-rota";
import { pode } from "@/app/sessao/permissoes";
import { useUsuario } from "@/app/sessao/sessao-contexto";
import { CarregandoPagina } from "@/app/shell/CarregandoPagina";
import { Pagina } from "@/app/shell/Pagina";
import { Breadcrumbs } from "@/ui/componentes/Breadcrumbs";
import { PRODUTOS } from "@/ui/componentes/produtos";

import { EstadoDaConsulta } from "../comum/EstadoDaConsulta";
import { AbasApolice } from "./AbasApolice";
import { ColunaLateral } from "./ColunaLateral";
import { DialogoAbrirSinistro } from "./DialogoAbrirSinistro";
import { DialogoCancelamento } from "./DialogoCancelamento";
import { DialogoEndosso } from "./DialogoEndosso";
import { FaceApolice, type AcoesDaFace } from "./FaceApolice";

type DialogoAberto = "endosso" | "cancelamento" | "sinistro" | null;

function useAcoes(apolice: ApoliceDto, abrir: (d: DialogoAberto) => void): AcoesDaFace {
  const usuario = useUsuario();
  const { data: features } = useFeatures();
  const baixar = useBaixarDocumento();
  const opera = pode(usuario.papel, "operarCarteira");
  const vigente = apolice.status === "Ativa" || apolice.status === "Programada";
  return {
    ...(features?.documentoPdf
      ? {
          baixarPdf: {
            executar: () => baixar.mutate({ id: apolice.id, numero: apolice.numero }),
            carregando: baixar.isPending,
          },
        }
      : {}),
    ...(opera && vigente && features?.endossoOnline ? { endossar: () => abrir("endosso") } : {}),
    ...(opera && vigente ? { cancelar: () => abrir("cancelamento") } : {}),
    // Programada ainda não começou: não há ocorrência possível dentro da vigência.
    ...(opera &&
    apolice.status !== "Programada" &&
    apolice.coberturas.some((c) => c.permiteSinistro)
      ? { abrirSinistro: () => abrir("sinistro") }
      : {}),
  };
}

function Conteudo({ apolice }: { apolice: ApoliceDto }) {
  const ref = useRef<HTMLDivElement>(null);
  useAnuncioDeRota(`Apólice ${apolice.numero}`, ref);
  const [dialogo, setDialogo] = useState<DialogoAberto>(null);
  const acoes = useAcoes(apolice, setDialogo);
  const fechar = (aberto: boolean) => {
    if (!aberto) setDialogo(null);
  };
  return (
    <div ref={ref} className="flex flex-col gap-4 md:gap-6">
      <Breadcrumbs
        itens={[
          { rotulo: "Apólices", href: "/apolices" },
          {
            rotulo: PRODUTOS[apolice.produtoCodigo].nome,
            href: `/apolices?produto=${apolice.produtoCodigo}`,
          },
          { rotulo: apolice.numero, codigo: true },
        ]}
      />
      <FaceApolice apolice={apolice} acoes={acoes} />
      <div className="grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-[minmax(0,1fr)_380px] lg:items-start">
        <AbasApolice
          apolice={apolice}
          aguardandoBoasVindas={aguardandoBoasVindas(apolice)}
          aoAbrirSinistro={acoes.abrirSinistro}
        />
        <ColunaLateral apolice={apolice} />
      </div>
      <DialogoEndosso apolice={apolice} aberto={dialogo === "endosso"} aoMudar={fechar} />
      <DialogoCancelamento apolice={apolice} aberto={dialogo === "cancelamento"} aoMudar={fechar} />
      <DialogoAbrirSinistro apolice={apolice} aberto={dialogo === "sinistro"} aoMudar={fechar} />
    </div>
  );
}

export default function ApoliceDetalhe() {
  const { id = "" } = useParams();
  const consulta = useApolice(id);
  if (consulta.isPending) return <CarregandoPagina />;
  if (consulta.isError) {
    return (
      <Pagina
        titulo="Apólice"
        migalhas={[{ rotulo: "Apólices", href: "/apolices" }, { rotulo: "Apólice" }]}
      >
        <EstadoDaConsulta
          erro={consulta.error}
          entidade="a apólice"
          voltarPara={{ rotulo: "Voltar às apólices", href: "/apolices" }}
          aoTentarDeNovo={() => void consulta.refetch()}
        />
      </Pagina>
    );
  }
  return <Conteudo apolice={consulta.data} />;
}

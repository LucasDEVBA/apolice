import { ArrowRight, Construction, Info } from "lucide-react";
import { useRef } from "react";
import { Link } from "react-router";

import { useAnuncioDeRota } from "@/app/anuncio-de-rota";
import { Button } from "@/ui/componentes/Button";
import { EmptyState } from "@/ui/componentes/EmptyState";
import { Gravura, Marca } from "@/ui/marca/Marca";

/** Login (fase 1): painel de marca + placeholder. Personas demo e formulário chegam com /auth. */
export default function Login() {
  const conteudo = useRef<HTMLElement>(null);
  useAnuncioDeRota("Entrar", conteudo);

  return (
    <div className="min-h-dvh bg-surface lg:grid lg:grid-cols-[480px_1fr] xl:grid-cols-[620px_1fr]">
      <section className="on-navy relative flex h-[200px] flex-col overflow-hidden border-b border-brand-line bg-brand px-4 py-6 text-on-navy md:px-14 lg:h-auto lg:min-h-dvh lg:border-r lg:border-b-0 lg:py-12">
        <Gravura tamanho="xl" className="absolute -right-[250px] -bottom-[250px] size-[760px]" />
        <Marca tamanhoSelo={36} classePalavra="text-[1.625rem]" className="relative" />
        <p className="relative mt-auto max-w-[480px] type-display-xl text-balance max-lg:text-[1.75rem] max-lg:leading-[1.15] lg:mt-32">
          Da cotação ao sinistro, cada apólice com histórico completo.
        </p>
        <p className="relative mt-5 hidden max-w-[450px] text-base leading-relaxed text-on-navy-2 lg:block">
          Plataforma de operação para corretoras: cálculo de prêmio auditável, emissão de Auto,
          Residencial e Vida, endossos, cancelamentos e regulação de sinistros.
        </p>
        <p className="relative mt-auto hidden gap-4 type-meta text-on-navy-3 lg:flex">
          <span>Projeto de portfólio</span>
          <span>Dados fictícios</span>
        </p>
      </section>

      <main ref={conteudo} id="conteudo" className="flex flex-col px-4 py-8 md:px-14 lg:py-10">
        <div className="mx-auto w-full max-w-[460px] lg:my-auto">
          <h1 tabIndex={-1} className="type-title-auth text-ink outline-none">
            Entrar
          </h1>
          <p className="mt-2.5 text-campo leading-relaxed text-ink-2">
            Escolha uma persona para explorar a plataforma com dados de demonstração, ou acesse com
            e-mail e senha.
          </p>
          <div className="mt-7 rounded-xl border border-line">
            <EmptyState
              icone={Construction}
              titulo="Em construção"
              descricao="As personas de demonstração e o formulário de acesso chegam com a integração do /auth (fase 2)."
              acao={
                <Button asChild iconeDireita={ArrowRight}>
                  <Link to="/">Explorar o layout</Link>
                </Button>
              }
            />
          </div>
          <p className="mt-5 flex items-start gap-2 type-meta text-ink-3">
            <Info size={15} className="mt-px shrink-0" aria-hidden />
            Ambiente de demonstração. Nenhum dado pessoal real é armazenado; os registros são
            recriados diariamente.
          </p>
        </div>
      </main>
    </div>
  );
}

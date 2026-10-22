import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, BookOpen, Car, CodeXml, Eye, EyeOff, Info, Lock, Mail } from "lucide-react";
import { useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router";
import { z } from "zod";

import { useFeatures } from "@/api/consultas/sistema";
import { ErroApi, mensagemDoErro } from "@/api/erros";
import { useAnuncioDeRota } from "@/app/anuncio-de-rota";
import { PERSONAS, type PersonaDemo } from "@/app/personas";
import type { EstadoNavegacaoLogin } from "@/app/sessao/RotaProtegida";
import { useSessao } from "@/app/sessao/sessao-contexto";
import { Avatar } from "@/ui/componentes/Avatar";
import { Badge } from "@/ui/componentes/Badge";
import { Button } from "@/ui/componentes/Button";
import { FormField } from "@/ui/componentes/FormField";
import { Input } from "@/ui/componentes/Input";
import { notificar } from "@/ui/componentes/notificar";
import { Skeleton } from "@/ui/componentes/Skeleton";
import { Money } from "@/ui/componentes/Valores";
import { Gravura, Marca } from "@/ui/marca/Marca";
import { cn } from "@/ui/utils/cn";

const REPOSITORIO = "https://github.com/LucasDEVBA/apolice";
const EMAIL_MAXIMO = 254;
const SENHA_MAXIMA = 128;

const esquema = z.object({
  email: z
    .string()
    .trim()
    .min(1, "Informe o e-mail.")
    .max(EMAIL_MAXIMO, "E-mail longo demais.")
    .pipe(z.email("Informe um e-mail válido, como nome@corretora.com.br.")),
  senha: z.string().min(1, "Informe a senha.").max(SENHA_MAXIMA, "Senha longa demais."),
});

type Credenciais = z.infer<typeof esquema>;

/** Cartão-espécime do painel de marca: ilustração rotulada, não um registro da base. */
function CartaoEspecime() {
  return (
    <figure
      aria-label="Exemplo ilustrativo de apólice"
      className="relative mt-auto hidden max-w-[420px] rounded-2xl border border-gold-line-soft bg-brand-deep/80 p-6 lg:block"
    >
      <div className="flex items-center justify-between gap-3">
        <span className="inline-flex items-center gap-2 text-label font-semibold text-gold-on-navy">
          <Car size={15} aria-hidden />
          Seguro Auto
        </span>
        <Badge tom="ok" sobreMarinho>
          Ativa
        </Badge>
      </div>
      <p className="mt-3 font-serif text-[1.375rem] leading-tight font-medium tracking-[-0.01em] text-on-navy">
        AUTO-2026-000042
      </p>
      <p className="mt-1 text-label text-on-navy-2">Exemplo ilustrativo · dados fictícios</p>
      <div aria-hidden className="mt-4 h-1 rounded-full bg-on-navy/10">
        <div className="h-full w-3/5 rounded-full bg-gold-line" />
      </div>
      <div className="mt-4 flex items-end justify-between gap-4 border-t border-on-navy/10 pt-4">
        <div>
          <p className="text-xs text-on-navy-3">Prêmio total</p>
          <Money valor={4862.38} variante="heroi" sobreMarinho className="text-2xl text-on-navy" />
        </div>
        <p className="text-right text-xs text-on-navy-3">
          Parcelamento
          <span className="block text-sm font-semibold text-on-navy tnum">10× de R$ 486,24</span>
        </p>
      </div>
    </figure>
  );
}

function PainelMarca() {
  return (
    <section
      aria-label="Apólice"
      className="on-navy relative flex h-[200px] flex-col overflow-hidden border-b border-brand-line bg-brand px-4 py-6 text-on-navy md:px-14 lg:h-auto lg:min-h-dvh lg:border-r lg:border-b-0 lg:py-12"
    >
      <Gravura tamanho="xl" className="absolute -right-[250px] -bottom-[250px] size-[760px]" />
      <Marca tamanhoSelo={36} classePalavra="text-[1.625rem]" className="relative" />
      <p className="relative mt-auto max-w-[480px] type-display-xl text-balance max-lg:text-[1.75rem] max-lg:leading-[1.15] lg:mt-28">
        Da cotação ao sinistro, cada apólice com histórico completo.
      </p>
      <p className="relative mt-5 hidden max-w-[450px] text-base leading-relaxed text-on-navy-2 lg:block">
        Plataforma de operação para corretoras: cálculo de prêmio auditável, emissão de Auto,
        Residencial e Vida, endossos, cancelamentos e regulação de sinistros.
      </p>
      <CartaoEspecime />
      <p className="relative mt-8 hidden gap-4 type-meta text-on-navy-3 lg:flex">
        <span>Projeto de portfólio</span>
        <span>Dados fictícios</span>
      </p>
    </section>
  );
}

interface ListaPersonasProps {
  aoEscolher: (persona: PersonaDemo) => void;
  entrando: string | null;
}

function ListaPersonas({ aoEscolher, entrando }: ListaPersonasProps) {
  return (
    <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line">
      {PERSONAS.map((persona) => (
        <li key={persona.id}>
          <button
            type="button"
            onClick={() => aoEscolher(persona)}
            aria-disabled={entrando !== null || undefined}
            aria-busy={entrando === persona.id || undefined}
            className="group flex min-h-[70px] w-full items-center gap-3.5 bg-surface px-4 py-3 text-left transition-colors duration-(--dur-fast) hover:bg-surface-hover focus-visible:-outline-offset-2 aria-disabled:cursor-progress"
          >
            <Avatar nome={persona.nome} variante={persona.avatar} tamanho="lg" />
            <span className="min-w-0 flex-1">
              <span className="block text-sm text-ink">
                <span className="font-semibold">{persona.nome}</span>
                <span className="text-ink-2"> · {persona.rotuloPapel}</span>
              </span>
              <span className="block type-meta text-ink-3">{persona.escopo}</span>
            </span>
            <ArrowRight
              size={18}
              className={cn(
                "shrink-0 text-ink-3 transition-transform duration-(--dur-fast) group-hover:translate-x-0.5 group-hover:text-ink",
                entrando === persona.id && "motion-safe:animate-pulse",
              )}
              aria-hidden
            />
            {entrando === persona.id && <span className="sr-only">Entrando…</span>}
          </button>
        </li>
      ))}
    </ul>
  );
}

function FormularioAcesso({ aoEntrar }: { aoEntrar: () => void }) {
  const { entrar } = useSessao();
  const [mostrarSenha, setMostrarSenha] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Credenciais>({
    resolver: zodResolver(esquema),
    defaultValues: { email: "", senha: "" },
    mode: "onTouched",
  });

  async function enviar(dados: Credenciais) {
    try {
      await entrar(dados.email, dados.senha);
      aoEntrar();
    } catch (erro) {
      // 401: e-mail e senha errados dão a mesma resposta (sem enumerar contas, SPEC §5).
      if (erro instanceof ErroApi && erro.naoAutorizado) {
        setError("root", { message: erro.detalhe ?? "E-mail ou senha inválidos." });
        return;
      }
      const { titulo, descricao } = mensagemDoErro(erro);
      setError("root", { message: descricao ?? titulo });
    }
  }

  return (
    <form noValidate onSubmit={(e) => void handleSubmit(enviar)(e)} className="flex flex-col gap-4">
      <FormField rotulo="E-mail" erro={errors.email?.message}>
        {(campo) => (
          <Input
            {...campo}
            {...register("email")}
            type="email"
            autoComplete="username"
            icone={Mail}
            placeholder="nome@corretora.com.br"
          />
        )}
      </FormField>
      <FormField rotulo="Senha" erro={errors.senha?.message}>
        {(campo) => (
          <Input
            {...campo}
            {...register("senha")}
            type={mostrarSenha ? "text" : "password"}
            autoComplete="current-password"
            icone={Lock}
            sufixo={
              <button
                type="button"
                onClick={() => setMostrarSenha((v) => !v)}
                aria-label={mostrarSenha ? "Ocultar senha" : "Mostrar senha"}
                aria-pressed={mostrarSenha}
                className="-mr-2 grid size-10 place-items-center rounded-md text-ink-2 hover:bg-hover-tint hover:text-ink"
              >
                {mostrarSenha ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
              </button>
            }
          />
        )}
      </FormField>
      {errors.root?.message && (
        <p
          role="alert"
          className="rounded-md border border-bad-line bg-bad-bg px-3.5 py-2.5 text-label text-bad"
        >
          {errors.root.message}
        </p>
      )}
      <Button
        type="submit"
        tamanho="lg"
        larguraTotal
        carregando={isSubmitting}
        rotuloCarregando="Entrando…"
      >
        Entrar
      </Button>
    </form>
  );
}

export default function Login() {
  const conteudo = useRef<HTMLElement>(null);
  useAnuncioDeRota("Entrar", conteudo);
  const { estado, entrarComoPersona } = useSessao();
  const features = useFeatures();
  const navegar = useNavigate();
  const local = useLocation();
  const [entrando, setEntrando] = useState<string | null>(null);
  const destino = (local.state as EstadoNavegacaoLogin | null)?.de ?? "/";

  if (estado.status === "autenticada" && entrando === null) {
    return <Navigate to={destino} replace />;
  }

  function seguir() {
    void navegar(destino, { replace: true });
  }

  async function entrarComo(persona: PersonaDemo) {
    if (entrando) return;
    setEntrando(persona.id);
    try {
      await entrarComoPersona(persona.id);
      seguir();
    } catch (erro) {
      const { titulo, descricao } = mensagemDoErro(erro);
      notificar.erro({
        titulo,
        ...(descricao ? { descricao } : {}),
        ...(erro instanceof ErroApi && erro.correlationId
          ? { correlationId: erro.correlationId }
          : {}),
      });
    } finally {
      setEntrando(null);
    }
  }

  const demoLigado = features.data?.loginDemo ?? false;

  return (
    <div className="min-h-dvh bg-surface lg:grid lg:grid-cols-[480px_1fr] xl:grid-cols-[620px_1fr]">
      <PainelMarca />
      <main ref={conteudo} id="conteudo" className="flex flex-col px-4 py-8 md:px-14 lg:py-10">
        <nav aria-label="Links do projeto" className="flex justify-end gap-5 text-label text-ink-2">
          <a
            href="/scalar/v1"
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 hover:text-ink hover:underline"
          >
            <BookOpen size={15} aria-hidden />
            Documentação da API
            <span className="sr-only">(abre em nova aba)</span>
          </a>
          <a
            href={REPOSITORIO}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 hover:text-ink hover:underline"
          >
            <CodeXml size={15} aria-hidden />
            Código no GitHub
            <span className="sr-only">(abre em nova aba)</span>
          </a>
        </nav>
        <div className="mx-auto mt-8 w-full max-w-[460px] lg:my-auto">
          <h1 tabIndex={-1} className="type-title-auth text-ink outline-none">
            Entrar
          </h1>
          <p className="mt-2.5 text-campo leading-relaxed text-ink-2">
            {demoLigado
              ? "Escolha uma persona para explorar a plataforma com dados de demonstração, ou acesse com e-mail e senha."
              : "Acesse com o seu e-mail e senha."}
          </p>

          {features.isPending && (
            <div aria-busy className="mt-7 flex flex-col gap-2">
              <span className="sr-only">Carregando opções de acesso…</span>
              <Skeleton className="h-[70px] w-full rounded-xl" />
              <Skeleton className="h-[70px] w-full rounded-xl" />
            </div>
          )}

          {demoLigado && (
            <section aria-labelledby="titulo-personas" className="mt-7">
              <div className="mb-2.5 flex items-baseline justify-between gap-3">
                <h2 id="titulo-personas" className="type-label text-ink">
                  Entrar como
                </h2>
                <span className="type-meta text-ink-3">Acesso de demonstração</span>
              </div>
              <ListaPersonas aoEscolher={(p) => void entrarComo(p)} entrando={entrando} />
              <div aria-hidden className="my-6 flex items-center gap-4 type-meta text-ink-3">
                <span className="h-px flex-1 bg-line" />
                ou com e-mail e senha
                <span className="h-px flex-1 bg-line" />
              </div>
            </section>
          )}

          <div className={cn(!demoLigado && "mt-7")}>
            <FormularioAcesso aoEntrar={seguir} />
          </div>

          <p className="mt-5 flex items-start gap-2 type-meta text-ink-3">
            <Info size={15} className="mt-px shrink-0" aria-hidden />
            Ambiente de demonstração com dados fictícios. Nenhum dado pessoal real é armazenado.
          </p>
        </div>
      </main>
    </div>
  );
}

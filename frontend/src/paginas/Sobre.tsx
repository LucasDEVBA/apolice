import { BookOpen, CodeXml, ExternalLink, FileJson } from "lucide-react";
import type { ReactNode } from "react";

import { Pagina } from "@/app/shell/Pagina";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";

const REPOSITORIO = "https://github.com/LucasDEVBA/apolice";

const STACK = [
  {
    camada: "Backend",
    itens:
      ".NET 10, Minimal APIs, EF Core 10 (escrita) + Dapper (leitura), FluentValidation, HybridCache, Microsoft.FeatureManagement",
  },
  {
    camada: "Mensageria",
    itens:
      "Outbox transacional → RabbitMQ (local) ou Amazon SQS (produção), consumidores idempotentes",
  },
  {
    camada: "Dados",
    itens: "PostgreSQL local e no CI; Aurora DSQL em produção (IAM auth, sem senha de banco)",
  },
  {
    camada: "Frontend",
    itens:
      "React 19, TypeScript strict, TanStack Query, React Router 7, React Hook Form + Zod, Tailwind CSS 4, Radix, Recharts",
  },
  {
    camada: "Qualidade",
    itens: "xUnit + Testcontainers + ArchUnitNET; Vitest + Testing Library + axe; Playwright",
  },
  {
    camada: "Operação",
    itens:
      "Serilog JSON com correlation id, OpenTelemetry, health checks, Docker, GitHub Actions com OIDC, Terraform",
  },
] as const;

const DECISOES = [
  {
    titulo: "Clean Architecture + DDD tático",
    texto:
      "Agregados (Cotação, ApoliceSeguro, Sinistro) com regras e eventos no domínio; a aplicação orquestra, a infraestrutura implementa portas.",
  },
  {
    titulo: "CQRS-lite",
    texto:
      "Escrita pelo EF Core com concorrência otimista; leituras e listagens por Dapper com paginação por cursor (keyset).",
  },
  {
    titulo: "Outbox transacional",
    texto:
      "Eventos de domínio viram mensagens na mesma transação: boas-vindas, triagem e notificações chegam de forma assíncrona e idempotente.",
  },
  {
    titulo: "Segurança por padrão",
    texto:
      "Access token curto só em memória, refresh rotativo em cookie HttpOnly, RBAC + autorização por recurso (recurso alheio responde 404).",
  },
  {
    titulo: "Strategy na tarifação",
    texto:
      "Uma estratégia por produto (Auto, Residencial, Vida); os fatores multiplicam só os itens com taxa, e a memória de cálculo é exibida na tela.",
  },
  {
    titulo: "Dados honestos",
    texto:
      "A interface só mostra o que a API fornece. Dados de demonstração são fictícios e rotulados.",
  },
] as const;

interface CaixaProps {
  x: number;
  y: number;
  largura: number;
  titulo: string;
  linhas: readonly string[];
  destaque?: boolean;
}

const ALTURA_LINHA = 18;

function Caixa({ x, y, largura, titulo, linhas, destaque = false }: CaixaProps) {
  const altura = 40 + linhas.length * ALTURA_LINHA;
  return (
    <g>
      <rect
        x={x}
        y={y}
        width={largura}
        height={altura}
        rx={10}
        className={destaque ? "fill-brand stroke-gold-line" : "fill-surface stroke-line-strong"}
        strokeWidth={1}
      />
      <text
        x={x + 14}
        y={y + 25}
        className={destaque ? "fill-on-navy" : "fill-ink"}
        fontSize={14.5}
        fontWeight={600}
      >
        {titulo}
      </text>
      {linhas.map((linha, i) => (
        <text
          key={linha}
          x={x + 14}
          y={y + 46 + i * ALTURA_LINHA}
          className={destaque ? "fill-on-navy-2" : "fill-ink-2"}
          fontSize={13}
        >
          {linha}
        </text>
      ))}
    </g>
  );
}

function Seta({ d, rotulo, x, y }: { d: string; rotulo?: string; x?: number; y?: number }) {
  return (
    <g>
      <path d={d} className="fill-none stroke-ink-3" strokeWidth={1.4} markerEnd="url(#seta)" />
      {rotulo && x !== undefined && y !== undefined && (
        <text x={x} y={y} className="fill-ink-3" fontSize={12} textAnchor="middle">
          {rotulo}
        </text>
      )}
    </g>
  );
}

/**
 * Diagrama da arquitetura em SVG inline (cores por token, legível nos dois temas). O <desc> e a
 * legenda abaixo dizem o mesmo em texto, para quem não vê o desenho.
 */
function DiagramaArquitetura() {
  return (
    <figure className="flex flex-col gap-4">
      {/* Rola na horizontal no mobile: focável para quem só usa teclado (axe scrollable-region-focusable). */}
      <div
        // Região rolável: o foco por teclado é o que permite rolar sem mouse (WCAG 2.1.1).
        // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex
        tabIndex={0}
        role="region"
        aria-label="Diagrama da arquitetura (role na horizontal)"
        className="relative overflow-x-auto rounded-lg border border-line bg-surface-2 p-3"
      >
        <svg
          viewBox="0 0 720 400"
          role="img"
          aria-labelledby="diagrama-titulo diagrama-desc"
          className="h-auto w-full min-w-[620px]"
        >
          <title id="diagrama-titulo">Arquitetura do Apólice</title>
          <desc id="diagrama-desc">
            O navegador acessa a Vercel, que encaminha /api para a API .NET numa Lambda. A API grava
            no Aurora DSQL e, na mesma transação, no outbox. O relay publica no SQS, e o worker
            Lambda consome as mensagens para enviar boas-vindas, notificações e a triagem de
            sinistros. O código segue a Clean Architecture: Domain, Application, Infrastructure e os
            hosts Api e Worker.
          </desc>
          <defs>
            <marker
              id="seta"
              viewBox="0 0 10 10"
              refX="9"
              refY="5"
              markerWidth="7"
              markerHeight="7"
              orient="auto-start-reverse"
            >
              <path d="M0 0 L10 5 L0 10 z" className="fill-ink-3" />
            </marker>
          </defs>
          <Caixa
            x={10}
            y={16}
            largura={160}
            titulo="Navegador"
            linhas={["React 19 + TanStack", "token só em memória"]}
          />
          <Caixa
            x={205}
            y={16}
            largura={150}
            titulo="Vercel"
            linhas={["SPA estática", "rewrite /api"]}
          />
          <Caixa
            x={390}
            y={10}
            largura={320}
            titulo="API · Lambda .NET 10"
            linhas={[
              "Minimal APIs · ProblemDetails",
              "JWT curto + refresh rotativo",
              "rate limit · idempotência",
            ]}
            destaque
          />
          <Caixa
            x={390}
            y={176}
            largura={150}
            titulo="Aurora DSQL"
            linhas={["dados + outbox", "mesma transação"]}
          />
          <Caixa
            x={570}
            y={176}
            largura={140}
            titulo="Amazon SQS"
            linhas={["fila + DLQ", "RabbitMQ local"]}
          />
          <Caixa
            x={390}
            y={300}
            largura={320}
            titulo="Worker · Lambda"
            linhas={["boas-vindas, notificações, triagem", "consumidores idempotentes"]}
            destaque
          />
          <Caixa
            x={10}
            y={150}
            largura={345}
            titulo="Clean Architecture"
            linhas={[
              "Domain: agregados, VOs, eventos",
              "Application: casos de uso e portas",
              "Infrastructure: EF Core, Dapper, filas",
              "Api / Worker: composição e hosts",
            ]}
          />
          <Seta d="M170 46 H199" />
          <Seta d="M355 46 H384" rotulo="/api/v1" x={370} y={36} />
          <Seta d="M465 110 V170" rotulo="EF Core" x={500} y={145} />
          <Seta d="M540 206 H564" />
          <Seta d="M640 250 V294" rotulo="consome" x={672} y={276} />
        </svg>
      </div>
      <figcaption className="type-meta text-ink-3">
        Local e no CI o mesmo desenho roda com PostgreSQL e RabbitMQ no Docker Compose; o relay do
        outbox publica as mensagens pendentes se a publicação imediata falhar.
      </figcaption>
    </figure>
  );
}

function LinkExterno({
  href,
  icone,
  children,
}: {
  href: string;
  icone: ReactNode;
  children: ReactNode;
}) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer"
      className="flex items-center gap-3 rounded-lg border border-line px-4 py-3 text-sm font-semibold text-ink hover:border-line-strong hover:bg-surface-2"
    >
      <span
        aria-hidden
        className="grid size-9 place-items-center rounded-lg bg-surface-2 text-ink-2"
      >
        {icone}
      </span>
      <span className="flex-1">{children}</span>
      <ExternalLink size={14} className="text-ink-3" aria-hidden />
      <span className="sr-only">(abre em nova aba)</span>
    </a>
  );
}

export default function Sobre() {
  return (
    <Pagina
      titulo="Sobre o projeto"
      subtitulo="Projeto de portfólio: cotação, emissão e gestão de apólices de seguro"
    >
      <div className="grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-[minmax(0,1fr)_340px] lg:items-start">
        <div className="flex flex-col gap-4 md:gap-6">
          <Card>
            <CardHeader titulo="O que é" />
            <CardBody className="flex flex-col gap-3 pt-3 text-sm leading-relaxed text-ink-2">
              <p>
                O Apólice é uma plataforma de operação para corretoras: cotação com memória de
                cálculo auditável, emissão de apólices de Auto, Residencial e Vida, endossos com
                ajuste pro rata, cancelamento com restituição e regulação de sinistros com triagem
                automática.
              </p>
              <p>
                É um projeto de portfólio construído para demonstrar, com código real e testado, um
                backend .NET 10 e um frontend React com padrões de produção. Todos os dados são
                fictícios; nenhum dado pessoal real é armazenado.
              </p>
            </CardBody>
          </Card>
          <Card>
            <CardHeader
              titulo="Arquitetura"
              subtitulo="Clean Architecture, outbox transacional e serverless na AWS"
            />
            <CardBody className="pt-4">
              <DiagramaArquitetura />
            </CardBody>
          </Card>
          <Card>
            <CardHeader titulo="Decisões" />
            <CardBody className="pt-3">
              <dl className="grid gap-5 md:grid-cols-2">
                {DECISOES.map((d) => (
                  <div key={d.titulo}>
                    <dt className="text-sm font-semibold text-ink">{d.titulo}</dt>
                    <dd className="mt-1 text-sm leading-relaxed text-ink-2">{d.texto}</dd>
                  </div>
                ))}
              </dl>
            </CardBody>
          </Card>
        </div>
        <aside aria-label="Stack e links" className="flex flex-col gap-4 md:gap-6">
          <Card>
            <CardHeader titulo="Links" />
            <CardBody className="flex flex-col gap-2 pt-3">
              <LinkExterno href={REPOSITORIO} icone={<CodeXml size={17} />}>
                Código no GitHub
              </LinkExterno>
              <LinkExterno href="/scalar/v1" icone={<BookOpen size={17} />}>
                Documentação da API (Scalar)
              </LinkExterno>
              <LinkExterno href="/openapi/v1.json" icone={<FileJson size={17} />}>
                Contrato OpenAPI 3.1
              </LinkExterno>
            </CardBody>
          </Card>
          <Card>
            <CardHeader titulo="Stack" />
            <CardBody className="pt-3">
              <dl className="flex flex-col gap-4">
                {STACK.map((s) => (
                  <div key={s.camada}>
                    <dt className="text-label font-semibold text-ink">{s.camada}</dt>
                    <dd className="mt-0.5 text-label leading-relaxed text-ink-2">{s.itens}</dd>
                  </div>
                ))}
              </dl>
            </CardBody>
          </Card>
        </aside>
      </div>
    </Pagina>
  );
}

import { ExternalLink } from "lucide-react";

import { Pagina } from "@/app/shell/Pagina";
import { Card, CardBody, CardHeader } from "@/ui/componentes/Card";

const CAMADAS = [
  {
    titulo: "Backend",
    texto:
      ".NET 10 com Minimal APIs, Clean Architecture, DDD tático e CQRS-lite (EF Core + Dapper).",
  },
  {
    titulo: "Mensageria",
    texto:
      "Outbox transacional para RabbitMQ (local) ou Amazon SQS (produção), com consumidores idempotentes.",
  },
  {
    titulo: "Frontend",
    texto:
      "React 19 + TypeScript strict, TanStack Query, React Router, Tailwind CSS 4 e Radix, WCAG 2.1 AA.",
  },
  {
    titulo: "Operação",
    texto:
      "OpenTelemetry, Serilog JSON com correlation id, health checks, Docker e CI/CD com OIDC.",
  },
] as const;

export default function Sobre() {
  return (
    <Pagina
      titulo="Sobre o projeto"
      subtitulo="Plataforma de portfólio: cotação, emissão e gestão de apólices de seguro"
    >
      <Card>
        <CardHeader
          titulo="Arquitetura"
          subtitulo="Dados de demonstração, recriados diariamente. Nenhum dado pessoal real é armazenado."
        />
        <CardBody>
          <dl className="grid gap-5 md:grid-cols-2">
            {CAMADAS.map((camada) => (
              <div key={camada.titulo}>
                <dt className="text-sm font-semibold text-ink">{camada.titulo}</dt>
                <dd className="mt-1 text-sm text-ink-2">{camada.texto}</dd>
              </div>
            ))}
          </dl>
          <p className="mt-6">
            <a
              href="/scalar/v1"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1.5 text-label font-semibold text-link hover:underline hover:underline-offset-[3px]"
            >
              Documentação da API (Scalar)
              <ExternalLink size={13} aria-hidden />
              <span className="sr-only">(abre em nova aba)</span>
            </a>
          </p>
        </CardBody>
      </Card>
    </Pagina>
  );
}

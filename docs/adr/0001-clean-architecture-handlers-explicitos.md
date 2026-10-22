# ADR 0001: Clean Architecture com handlers explícitos, sem MediatR

- **Status:** aceita
- **Data:** 2026-10

## Contexto

O domínio de seguros tem regra de negócio de verdade (tarifação, endosso pro rata, cancelamento por prazo curto, máquina de estados do sinistro) que precisa ser testada sem banco, sem HTTP e sem broker. O mesmo caso de uso roda em dois hosts (a API HTTP e o Worker que consome eventos), e o banco muda entre ambientes (PostgreSQL local, Aurora DSQL em produção).

A escolha mais comum em .NET para despachar casos de uso é o MediatR. Ele tem dois custos aqui: a chamada `mediator.Send(command)` esconde quem trata o comando (o "ir para a definição" da IDE para na interface), e o MediatR, como o AutoMapper e o FluentAssertions, passou a ter licença comercial em 2025.

## Decisão

Quatro projetos, com dependência sempre para dentro:

| Projeto | Depende de | Conteúdo |
|---|---|---|
| `Apolice.Domain` | só BCL | agregados, value objects, eventos de domínio, `Result`/`Error`, tarifação |
| `Apolice.Application` | Domain + FluentValidation | casos de uso, portas (interfaces de repositório, consultas, segurança, serviços) |
| `Apolice.Infrastructure` | Application | EF Core, Dapper, Npgsql/DSQL, RabbitMQ/SQS, JWT, cache, PDF |
| `Apolice.Api` / `Apolice.Worker` | Infrastructure | composição e borda (HTTP, mensagens, Lambda) |

Os casos de uso são **handlers explícitos** com contratos próprios em `Application/Abstractions/Messaging`:

```csharp
public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
```

`AddApplication()` registra cada handler pelo tipo concreto e por cada interface fechada que ele implementa, por varredura do assembly. O endpoint recebe o handler concreto por injeção e chama `HandleAsync`. O `Result` volta para a borda, que o traduz em ProblemDetails.

O que um pipeline de *behaviors* do MediatR faria fica em lugares explícitos:

- validação de entrada e `Idempotency-Key`: filtros de endpoint na API;
- correlation id, tratamento de exceção, headers: middlewares;
- autorização por recurso: chamada explícita no handler (`AcessoUsuario.AutorizarCarteira`, `AutorizarAnalise`);
- auditoria e commit: o handler chama `IAuditoria.Registrar` e `IUnitOfWork.SaveChangesAsync`;
- eventos de domínio para o outbox: interceptor do EF Core, que não depende do handler lembrar.

Mapeamento entre entidade e DTO é manual (métodos `De`/`ParaDto`), sem AutoMapper.

## Alternativas consideradas

- **MediatR com pipeline behaviors.** Menos código repetido para preocupações transversais, mas comportamento implícito, navegação indireta e licença comercial.
- **Mediator próprio (um `ISender` caseiro).** Mantém a indireção sem ganhar nada que a DI já não dê.
- **Lógica nos endpoints ou em "services" por entidade.** Mais rápido no começo, mas mistura HTTP com regra e não serve ao Worker.

## Consequências

- Do endpoint, um F12 leva direto ao handler, e cada dependência dele aparece no construtor primário.
- Há repetição consciente: todo handler de mutação segue o mesmo roteiro (autenticar, carregar, autorizar, chamar o domínio, auditar, salvar). A repetição é curta e cada passo fica visível em revisão.
- `DependencyInjectionTests` conta os handlers (40: 32 casos de uso e 8 consumidores de evento) e confere que todos são `sealed`, terminam em `Handler` e resolvem pela DI. Esquecer o registro de um caso de uso quebra um teste, não a produção.
- A regra de dependência da SPEC §3 é verificada por testes de arquitetura em ArchUnitNET (`Apolice.ArchitectureTests`: regras entre as camadas, controles positivos e as convenções de handler e de agregado) e por um teste de reflexão sobre as referências do assembly da Application (`Deve_referenciar_so_dominio_e_abstracoes_quando_compila_a_aplicacao`).

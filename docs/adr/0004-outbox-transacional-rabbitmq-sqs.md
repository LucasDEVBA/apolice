# ADR 0004: Outbox transacional, RabbitMQ local, SQS em produção, consumidores idempotentes

- **Status:** aceita
- **Data:** 2026-10

## Contexto

Emitir, endossar e cancelar apólice e mudar o status de um sinistro geram efeitos que não precisam acontecer na mesma requisição: e-mail ao segurado (simulado) e triagem automática do sinistro. Fazer isso de forma assíncrona tem dois riscos clássicos:

- **escrita dupla:** gravar no banco e depois publicar no broker. Se o processo cai entre os dois, ou a mensagem se perde (gravou e não publicou) ou anuncia algo que não existe (publicou e o commit falhou);
- **entrega repetida:** todo broker prático entrega pelo menos uma vez, e o consumidor pode processar duas vezes a mesma mensagem.

O broker também muda por ambiente: RabbitMQ no `docker compose` e no CI, Amazon SQS em produção (serverless, sem servidor de broker para manter).

## Decisão

**Produtor: outbox na mesma transação do agregado.**
- Os agregados só levantam eventos de domínio (`Raise`). O `DomainEventsToOutboxInterceptor` (EF Core, `SavingChanges`) transforma cada evento numa linha de `outbox_mensagens` dentro do mesmo `SaveChanges`. O id da linha é o `EventoId` do evento, o tipo é o nome da classe e o payload é o JSON do tipo concreto, junto com o `traceparent` e o correlation id da requisição.
- **Despacho imediato** (`Outbox:DispatchImediato`, ligado só na API): o `OutboxDispatchImediatoInterceptor` publica, depois do commit, as mensagens que aquele `SaveChanges` gravou e as marca como processadas com `ExecuteUpdateAsync`. É best-effort: falha vira log, e a linha continua pendente.
- **Relay** (`OutboxDispatcher.DespacharPendentesAsync`): publica as pendentes em ordem de ocorrência, marca cada uma isoladamente e, em caso de falha, incrementa `tentativas` e grava `ultimo_erro`. Roda no Worker local a cada 2 s e numa Lambda agendada pelo EventBridge a cada 5 min em produção.
- `IMessageBus.PublishAsync` só retorna depois de o broker confirmar (publisher confirm no RabbitMQ, resposta do `SendMessage` no SQS), então só se marca o que de fato saiu.

**Transporte** (`Messaging:Transport`):
- **RabbitMQ:** exchange topic `apolice.eventos`, fila quorum `apolice.worker` com `x-delivery-limit` 5 e dead-letter para `apolice.eventos.dlx` e `apolice.worker.dlq`. Prefetch 10 e ack só depois do commit. Uma falha é republicada com um contador próprio de tentativas e espera crescente; na 5ª, `nack` sem requeue manda a mensagem para a DLQ. Isso existe porque, no RabbitMQ 4, o `nack` com requeue não incrementa o contador de entregas da fila quorum, e a mensagem voltaria para sempre.
- **SQS:** o corpo é o payload e os metadados vão em atributos (`tipo`, `mensagemId`, `ocorridoEm`, `traceparent`, `correlationId`). O id do outbox viaja em `mensagemId` porque o `MessageId` do SQS muda a cada reenvio. A Lambda consumidora recebe lotes de 10, devolve falhas parciais (`ReportBatchItemFailures`) e, depois de 5 recebimentos, a mensagem vai para a DLQ, que guarda 14 dias e dispara alarme por e-mail.
- **InMemory:** despacha direto para o consumidor, para testes.

**Consumidor: inbox na mesma transação do efeito.**
- `ProcessadorMensagemIntegracao` cria um escopo de DI por mensagem (DbContext próprio), propaga o correlation id e usa o `traceparent` como pai do span de consumo.
- `IntegrationEventDispatcher` resolve o tipo pelo nome e chama cada `IIntegrationEventHandler<T>`. Para cada handler: se `(mensagem_id, consumidor)` já está em `mensagens_processadas`, pula; senão, executa, registra o par e salva, numa transação só.
- Tipo desconhecido lança exceção (a mensagem volta e, esgotadas as entregas, vai para a DLQ, de onde pode ser reenviada, por exemplo depois de um deploy defasado entre API e Worker). Tipo conhecido sem consumidor, como `TaxaCoberturaAtualizada`, é confirmado.

## Alternativas consideradas

- **Publicar direto depois do `SaveChanges`.** É exatamente a escrita dupla.
- **Transação distribuída entre banco e broker.** Nem o SQS nem o DSQL participam de 2PC.
- **CDC lendo o log do banco (Debezium e afins).** Infraestrutura pesada para o porte, e dependente de recursos de replicação do banco.
- **Biblioteca de mensageria (MassTransit, NServiceBus, Wolverine).** Resolveriam outbox, retry e topologia, mas esconderiam o que o projeto quer mostrar e somariam uma dependência grande. O NServiceBus é comercial.
- **Fila SQS FIFO.** Ordem e deduplicação por 5 minutos, mas com vazão menor e sem dispensar a idempotência do consumidor: a janela de deduplicação não cobre um relay que republica depois.

## Consequências

- Entrega **pelo menos uma vez** de ponta a ponta: o relay e o despacho imediato podem publicar a mesma linha, e o consumidor pode cair depois do efeito e antes do ack. O inbox absorve as duas situações, e o handler de boas-vindas ainda tem uma segunda barreira (`BoasVindasEnviadasEm`).
- A **ordem** de entrega não é garantida (despacho imediato e relay podem se cruzar, e a fila SQS é standard). Cada consumidor trata um evento isolado e carrega o estado atual do agregado do banco.
- Em produção, se o despacho imediato falhar, o evento espera até 5 min pelo relay.
- Um consumidor que grava num agregado versionado pode receber conflito de concorrência; a exceção devolve a mensagem para nova tentativa.
- A tabela `outbox_mensagens` cresce: marcar como processada não apaga. Uma rotina de limpeza das linhas antigas ainda não existe.
- Os testes de integração cobrem o outbox gravado com `traceparent` e correlação, a ausência de outbox quando o save falha, o relay até a notificação, a falha de publicação contando tentativa, o despacho imediato e o caminho até a DLQ no RabbitMQ real.

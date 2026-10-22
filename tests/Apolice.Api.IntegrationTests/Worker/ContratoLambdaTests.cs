using System.Reflection;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Apolice.Api.IntegrationTests.Worker.Suporte;
using Apolice.Application.Abstractions.Security;
using Apolice.Infrastructure.Messaging.Sqs;
using Apolice.Worker.Hospedagem;
using Apolice.Worker.Lambda;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apolice.Api.IntegrationTests.Worker;

// O que só quebraria no deploy: strings de handler do Terraform, serializer da Lambda, nomes JSON da resposta de falha
// parcial e a forma do evento SQS que o runtime entrega. Também a composição do host (sem banco: nada aqui conecta).
public sealed class ContratoLambdaTests
{
    [Theory]
    [InlineData("Apolice.Worker::Apolice.Worker.Lambda.SqsFunction::HandleAsync", typeof(SQSEvent), typeof(Task<SQSBatchResponse>))]
    [InlineData("Apolice.Worker::Apolice.Worker.Lambda.OutboxRelayFunction::HandleAsync", typeof(Stream), typeof(Task<ResultadoRelay>))]
    public void Deve_resolver_o_handler_como_o_runtime_da_lambda_quando_usa_a_string_do_terraform(string handler, Type entrada, Type retorno)
    {
        var partes = handler.Split("::");
        var assembly = typeof(SqsFunction).Assembly;
        assembly.GetName().Name.ShouldBe(partes[0]);

        var tipo = assembly.GetType(partes[1]).ShouldNotBeNull();
        tipo.GetConstructor(Type.EmptyTypes).ShouldNotBeNull("o runtime instancia o handler pelo construtor sem parâmetros");
        var metodo = tipo.GetMethod(partes[2], BindingFlags.Public | BindingFlags.Instance).ShouldNotBeNull();
        metodo.GetParameters().Select(p => p.ParameterType).ShouldBe([entrada, typeof(ILambdaContext)]);
        metodo.ReturnType.ShouldBe(retorno);
        assembly.GetCustomAttribute<LambdaSerializerAttribute>().ShouldNotBeNull().SerializerType.ShouldBe(typeof(DefaultLambdaJsonSerializer));
    }

    [Fact]
    public void Deve_serializar_a_resposta_com_os_nomes_que_o_sqs_espera_quando_ha_falha_parcial()
    {
        var resposta = new SQSBatchResponse([new SQSBatchResponse.BatchItemFailure { ItemIdentifier = "id-1" }]);
        using var saida = new MemoryStream();

        new DefaultLambdaJsonSerializer().Serialize(resposta, saida);

        Encoding.UTF8.GetString(saida.ToArray()).ShouldBe("""{"batchItemFailures":[{"itemIdentifier":"id-1"}]}""");
    }

    [Fact]
    public void Deve_ler_o_envelope_quando_o_evento_chega_no_formato_json_do_runtime()
    {
        var mensagemId = Guid.CreateVersion7();
        var json = $$$"""
            {"Records":[{"messageId":"sqs-1","receiptHandle":"r","body":"{\"numero\":\"AUTO-2026-000001\"}",
              "attributes":{"ApproximateReceiveCount":"1"},
              "messageAttributes":{
                "tipo":{"stringValue":"ApoliceEmitida","dataType":"String"},
                "mensagemId":{"stringValue":"{{{mensagemId}}}","dataType":"String"},
                "correlationId":{"stringValue":"corr-sqs","dataType":"String"}},
              "eventSource":"aws:sqs"}]}
            """;
        using var entrada = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var registro = new DefaultLambdaJsonSerializer().Deserialize<SQSEvent>(entrada).Records.ShouldHaveSingleItem();
        var atributos = registro.MessageAttributes.ToDictionary(a => a.Key, a => (string?)a.Value.StringValue, StringComparer.Ordinal);
        var mensagem = EnvelopeSqs.Ler(registro.Body, atributos).ShouldNotBeNull();

        mensagem.Id.ShouldBe(mensagemId);
        mensagem.Tipo.ShouldBe("ApoliceEmitida");
        mensagem.Payload.ShouldBe("""{"numero":"AUTO-2026-000001"}""");
        mensagem.CorrelationId.ShouldBe("corr-sqs");
    }

    [Fact]
    public async Task Deve_usar_o_usuario_de_sistema_nao_autenticado_quando_compoe_o_worker()
    {
        var host = HostWorker.Criar(HostWorker.SemBanco);
        try
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var usuario = escopo.ServiceProvider.GetRequiredService<ICurrentUser>();

            usuario.Autenticado.ShouldBeFalse();
            usuario.UsuarioId.ShouldBeNull();
            usuario.Papel.ShouldBeNull();
        }
        finally
        {
            await HostWorker.DescartarAsync(host);
        }
    }

    [Theory]
    [InlineData("RabbitMq", new[] { typeof(OutboxRelayService), typeof(RabbitMqConsumerService) })]
    [InlineData("InMemory", new[] { typeof(OutboxRelayService) })]
    public async Task Deve_hospedar_o_consumidor_so_com_rabbitmq_quando_monta_o_host_local(string transporte, Type[] esperados)
    {
        var host = HostWorker.Criar(HostWorker.SemBanco, new Dictionary<string, string?> { ["Messaging:Transport"] = transporte }, hostLocal: true);
        try
        {
            var hospedados = host.Services.GetServices<IHostedService>().Select(s => s.GetType()).Where(t => t.Assembly == typeof(OutboxRelayService).Assembly);

            hospedados.ShouldBe(esperados, ignoreOrder: true);
        }
        finally
        {
            await HostWorker.DescartarAsync(host);
        }
    }
}

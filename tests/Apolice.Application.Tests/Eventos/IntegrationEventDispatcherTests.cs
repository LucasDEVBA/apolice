using System.Text.Json;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices.Cancelar;
using Apolice.Application.Features.Eventos;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apolice.Application.Tests.Eventos;

public sealed class IntegrationEventDispatcherTests : TesteBase
{
    private IIntegrationEventDispatcher Dispatcher => Amb.Servico<IIntegrationEventDispatcher>();

    // Consumidor sem segunda barreira (só notifica): a única coisa que impede o e-mail duplicado é o inbox.
    [Fact]
    public async Task Deve_aplicar_o_efeito_uma_unica_vez_quando_a_mesma_mensagem_chega_duas_vezes()
    {
        var evento = await CancelamentoAsync();
        var payload = JsonSerializer.Serialize(evento, JsonOpcoes.Padrao);

        await Dispatcher.DispatchAsync(evento.EventoId, nameof(ApoliceCancelada), payload, Ct);
        await Dispatcher.DispatchAsync(evento.EventoId, nameof(ApoliceCancelada), payload, Ct);

        Amb.Banco.Notificacoes.Count(n => n.Tipo == TipoNotificacao.Cancelamento).ShouldBe(1);
        Amb.Banco.MensagensProcessadas.Where(m => m.MensagemId == evento.EventoId).ShouldBe([(evento.EventoId, nameof(NotificarApoliceCanceladaHandler))]);
    }

    [Theory]
    [InlineData("EventoQueNaoExiste")]
    [InlineData(null)]
    public async Task Deve_recusar_para_a_mensagem_ir_a_dlq_quando_tipo_desconhecido_ou_nulo(string? tipo)
    {
        var savesAntes = Amb.Banco.TotalSaves;

        await Should.ThrowAsync<MensagemDesconhecidaException>(() => Dispatcher.DispatchAsync(Guid.NewGuid(), tipo!, "{}", Ct));

        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Banco.MensagensProcessadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deve_confirmar_sem_efeito_quando_tipo_conhecido_nao_tem_consumidor()
    {
        var evento = new TaxaCoberturaAtualizada(CodigosProduto.Auto, CodigosCobertura.Casco, 0.032m, 0.035m, Amb.Agora);
        var savesAntes = Amb.Banco.TotalSaves;

        await Dispatcher.DispatchAsync(evento.EventoId, nameof(TaxaCoberturaAtualizada), JsonSerializer.Serialize(evento, JsonOpcoes.Padrao), Ct);

        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Logs.Mensagens.ShouldContain(m => m.Contains("nenhum consumidor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deve_propagar_sem_registrar_no_inbox_quando_payload_nulo_ou_invalido()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Dispatcher.DispatchAsync(Guid.NewGuid(), nameof(SinistroPago), "null", Ct));
        await Should.ThrowAsync<JsonException>(() => Dispatcher.DispatchAsync(Guid.NewGuid(), nameof(SinistroPago), "{ quebrado", Ct));

        Amb.Banco.MensagensProcessadas.ShouldBeEmpty();
    }

    // Tipo e payload trocados (bug no relay): o JSON de outro evento vira um SinistroPago vazio, que o consumidor recusa.
    [Fact]
    public async Task Deve_propagar_sem_registrar_no_inbox_quando_payload_e_de_outro_evento()
    {
        var outro = new ApoliceCancelada(Guid.NewGuid(), "AUTO-2026-000001", MotivoCancelamento.Inadimplencia, 10m, "Maria", Dados.EmailSegurado, Amb.Agora);
        var mensagemId = Guid.NewGuid();

        await Should.ThrowAsync<InvalidOperationException>(() => Dispatcher.DispatchAsync(mensagemId, nameof(SinistroPago), JsonSerializer.Serialize(outro, JsonOpcoes.Padrao), Ct));

        Amb.Banco.MensagensProcessadas.ShouldBeEmpty();
        Amb.Banco.Notificacoes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deve_propagar_o_conflito_sem_registrar_no_inbox_e_concluir_na_reentrega()
    {
        var evento = await CancelamentoAsync();
        var payload = JsonSerializer.Serialize(evento, JsonOpcoes.Padrao);
        Amb.Banco.FalharProximoSaveComConflito = true;

        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() => Dispatcher.DispatchAsync(evento.EventoId, nameof(ApoliceCancelada), payload, Ct));
        Amb.Banco.MensagensProcessadas.ShouldBeEmpty();
        Amb.Banco.Notificacoes.ShouldBeEmpty();

        await Dispatcher.DispatchAsync(evento.EventoId, nameof(ApoliceCancelada), payload, Ct);

        Amb.Banco.Notificacoes.ShouldHaveSingleItem().Tipo.ShouldBe(TipoNotificacao.Cancelamento);
        Amb.Banco.MensagensProcessadas.ShouldHaveSingleItem();
    }

    // Ordem explícita dos consumidores (independe da ordem de varredura do assembly): o primeiro conclui, o segundo
    // falha; na reentrega o primeiro é pulado pelo inbox e só o segundo roda.
    [Fact]
    public async Task Deve_pular_o_consumidor_ja_concluido_e_refazer_so_o_que_falhou_quando_mensagem_e_reentregue()
    {
        var banco = new BancoEmMemoria();
        var primeiro = new PrimeiroConsumidorDeTesteHandler();
        var segundo = new SegundoConsumidorDeTesteHandler { FalharNaPrimeiraVez = true };
        using var servicos = new ServiceCollection()
            .AddSingleton<IIntegrationEventHandler<SinistroPago>>(primeiro)
            .AddSingleton<IIntegrationEventHandler<SinistroPago>>(segundo)
            .BuildServiceProvider();
        var evento = new SinistroPago(Guid.NewGuid(), "SIN-2026-000001", Dados.EmailSegurado, Amb.Agora);
        var payload = JsonSerializer.Serialize(evento, JsonOpcoes.Padrao);

        await Should.ThrowAsync<InvalidOperationException>(() => NovoDispatcher(servicos, banco).DispatchAsync(evento.EventoId, nameof(SinistroPago), payload, Ct));
        banco.MensagensProcessadas.ShouldBe([(evento.EventoId, nameof(PrimeiroConsumidorDeTesteHandler))]);

        await NovoDispatcher(servicos, banco).DispatchAsync(evento.EventoId, nameof(SinistroPago), payload, Ct);

        primeiro.Chamadas.ShouldBe(1);
        segundo.Chamadas.ShouldBe(2);
        banco.MensagensProcessadas.Select(m => m.Consumidor).Order(StringComparer.Ordinal)
            .ShouldBe([nameof(PrimeiroConsumidorDeTesteHandler), nameof(SegundoConsumidorDeTesteHandler)]);
    }

    // Com os consumidores reais: qualquer que seja a ordem, a reentrega deixa notificação e triagem feitas uma vez só.
    [Fact]
    public async Task Deve_concluir_os_dois_consumidores_uma_vez_quando_um_falha_e_a_mensagem_volta()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.ProcessarOutboxAsync();
        await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        var evento = Amb.Banco.Outbox.OfType<SinistroAberto>().Single();
        Amb.Banco.Outbox.Clear();
        var payload = JsonSerializer.Serialize(evento, JsonOpcoes.Padrao);
        var apoliceGravada = Amb.Banco.Apolices.Single();
        Amb.Banco.Apolices.Clear();

        // A triagem precisa da apólice e falha; a mensagem volta para a fila.
        await Should.ThrowAsync<InvalidOperationException>(() => Dispatcher.DispatchAsync(evento.EventoId, nameof(SinistroAberto), payload, Ct));
        Amb.Banco.MensagensProcessadas.ShouldNotContain((evento.EventoId, nameof(TriarSinistroAbertoHandler)));

        Amb.Banco.Apolices.Add(apoliceGravada);
        await Dispatcher.DispatchAsync(evento.EventoId, nameof(SinistroAberto), payload, Ct);

        Amb.Banco.Notificacoes.Count(n => n.Tipo == TipoNotificacao.SinistroAberto).ShouldBe(1);
        Amb.Banco.Sinistros.Single().Triagem.ShouldNotBeNull();
        Amb.Banco.MensagensProcessadas.Where(m => m.MensagemId == evento.EventoId).Select(m => m.Consumidor).Order(StringComparer.Ordinal)
            .ShouldBe([nameof(NotificarSinistroAbertoHandler), nameof(TriarSinistroAbertoHandler)]);
    }

    [Fact]
    public void Deve_conhecer_todos_os_eventos_do_dominio_quando_monta_o_mapa_de_tipos()
    {
        IntegrationEventDispatcher.TiposConhecidos.Order(StringComparer.Ordinal).ShouldBe(
        [
            nameof(ApoliceCancelada), nameof(ApoliceEmitida), nameof(ApoliceEndossada), nameof(SinistroAberto),
            nameof(SinistroAprovado), nameof(SinistroNegado), nameof(SinistroPago), nameof(TaxaCoberturaAtualizada),
        ]);
    }

    [Fact]
    public void Deve_oferecer_os_construtores_padrao_quando_mensagem_desconhecida()
    {
        var interna = new FormatException();

        new MensagemDesconhecidaException().Message.ShouldNotBeNullOrWhiteSpace();
        new MensagemDesconhecidaException("msg").Message.ShouldBe("msg");
        new MensagemDesconhecidaException("msg", interna).InnerException.ShouldBeSameAs(interna);
    }

    private static IntegrationEventDispatcher NovoDispatcher(IServiceProvider servicos, BancoEmMemoria banco)
    {
        var sessao = new SessaoBanco(banco);
        return new IntegrationEventDispatcher(servicos, sessao, sessao, TimeProvider.System, NullLogger<IntegrationEventDispatcher>.Instance);
    }

    private async Task<ApoliceCancelada> CancelamentoAsync()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.Servico<CancelarApoliceHandler>().HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.Inadimplencia), Ct);
        return Amb.Banco.Outbox.OfType<ApoliceCancelada>().Single();
    }

    private sealed class PrimeiroConsumidorDeTesteHandler : IIntegrationEventHandler<SinistroPago>
    {
        public int Chamadas { get; private set; }

        public Task HandleAsync(SinistroPago evento, CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.CompletedTask;
        }
    }

    private sealed class SegundoConsumidorDeTesteHandler : IIntegrationEventHandler<SinistroPago>
    {
        public bool FalharNaPrimeiraVez { get; init; }

        public int Chamadas { get; private set; }

        public Task HandleAsync(SinistroPago evento, CancellationToken cancellationToken)
        {
            Chamadas++;
            return FalharNaPrimeiraVez && Chamadas == 1
                ? Task.FromException(new InvalidOperationException("Falha transitória do segundo consumidor."))
                : Task.CompletedTask;
        }
    }
}

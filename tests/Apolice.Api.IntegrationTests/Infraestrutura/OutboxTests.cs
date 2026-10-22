using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Common;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Outbox;
using Apolice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

public sealed class OutboxTests(PostgresFixture postgres) : TesteComBanco(postgres, "outbox")
{
    [Fact]
    public async Task Deve_gravar_o_evento_no_outbox_com_traceparent_e_correlacao_quando_salvar_o_agregado()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        using var atividade = new Activity("teste.emitir").Start();

        var apolice = await NoEscopoAsync(async sp =>
        {
            sp.GetRequiredService<ContextoCorrelacao>().Definir("corr-outbox-1");
            var cenarios = new Cenarios(sp);
            var emitida = await cenarios.ApoliceAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            emitida.DomainEvents.ShouldNotBeEmpty();
            await cenarios.Uow.SaveChangesAsync(Ct);
            emitida.DomainEvents.ShouldBeEmpty();
            return emitida;
        });

        var mensagem = await Ambiente.ExecutarAsync<ApoliceDbContext, OutboxMessage>(db => db.Outbox.AsNoTracking().SingleAsync(Ct));
        mensagem.Tipo.ShouldBe(nameof(ApoliceEmitida));
        mensagem.CorrelationId.ShouldBe("corr-outbox-1");
        mensagem.Traceparent.ShouldBe(atividade.Id);
        mensagem.ProcessadoEm.ShouldBeNull();
        var evento = JsonSerializer.Deserialize<ApoliceEmitida>(mensagem.Payload, JsonOpcoes.Padrao)!;
        evento.EventoId.ShouldBe(mensagem.Id);
        evento.ApoliceId.ShouldBe(apolice.Id);
        evento.Numero.ShouldBe(apolice.Numero);
    }

    [Fact]
    public async Task Deve_nao_gravar_outbox_quando_o_save_do_agregado_falha()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var primeira = await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            var apolice = await cenarios.ApoliceAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            await cenarios.Uow.SaveChangesAsync(Ct);
            return apolice;
        });
        var antes = await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Outbox.CountAsync(Ct));

        // Mesmo número de apólice: o índice único derruba a transação inteira, outbox incluído.
        await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            var cotacao = await cenarios.CotacaoAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            cotacao.Aceitar(Ambiente.Tempo.GetUtcNow());
            var repetida = ApoliceSeguro.Emitir(cotacao, primeira.Numero, primeira.Vigencia.Inicio, 1, Ambiente.Tempo.GetUtcNow()).Value;
            sp.GetRequiredService<IApoliceRepository>().Adicionar(repetida);
            await Should.ThrowAsync<DbUpdateException>(() => cenarios.Uow.SaveChangesAsync(Ct));
        });

        (await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Outbox.CountAsync(Ct))).ShouldBe(antes);
        (await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Apolices.CountAsync(Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task Deve_publicar_marcar_processada_e_gerar_notificacao_quando_o_relay_roda()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var apoliceId = await EmitirAsync(ana.Id);

        var publicadas = await Ambiente.ExecutarAsync<OutboxDispatcher, int>(d => d.DespacharPendentesAsync(10, Ct));

        publicadas.ShouldBe(1);
        var mensagem = await Ambiente.ExecutarAsync<ApoliceDbContext, OutboxMessage>(db => db.Outbox.AsNoTracking().SingleAsync(Ct));
        mensagem.ProcessadoEm.ShouldNotBeNull();
        mensagem.Tentativas.ShouldBe(1);
        var notificacao = await Ambiente.ExecutarAsync<ApoliceDbContext, Notificacao>(db => db.Notificacoes.AsNoTracking().SingleAsync(Ct));
        notificacao.Tipo.ShouldBe(TipoNotificacao.BoasVindas);
        notificacao.ReferenciaId.ShouldBe(apoliceId);
        var apolice = await Ambiente.ExecutarAsync<ApoliceDbContext, ApoliceSeguro>(db => db.Apolices.AsNoTracking().SingleAsync(Ct));
        apolice.BoasVindasEnviadasEm.ShouldNotBeNull();
        (await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.MensagensProcessadas.CountAsync(m => m.MensagemId == mensagem.Id, Ct))).ShouldBe(1);

        (await Ambiente.ExecutarAsync<OutboxDispatcher, int>(d => d.DespacharPendentesAsync(10, Ct))).ShouldBe(0);
    }

    [Fact]
    public async Task Deve_contar_tentativa_e_guardar_erro_quando_a_publicacao_falha()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        await EmitirAsync(ana.Id);
        var falhando = new BusFalho();
        await using var escopo = Ambiente.Provedor.CreateAsyncScope();
        var dispatcher = ActivatorUtilities.CreateInstance<OutboxDispatcher>(escopo.ServiceProvider, (IMessageBus)falhando);

        (await dispatcher.DespacharPendentesAsync(10, Ct)).ShouldBe(0);
        (await dispatcher.DespacharPendentesAsync(10, Ct)).ShouldBe(0);

        var mensagem = await Ambiente.ExecutarAsync<ApoliceDbContext, OutboxMessage>(db => db.Outbox.AsNoTracking().SingleAsync(Ct));
        mensagem.ProcessadoEm.ShouldBeNull();
        mensagem.Tentativas.ShouldBe(2);
        mensagem.UltimoErro.ShouldBe("InvalidOperationException: broker fora do ar");
    }

    private async Task<Guid> EmitirAsync(Guid corretorId) =>
        await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            var apolice = await cenarios.ApoliceAutoAsync(corretorId, Ambiente.Tempo.GetUtcNow());
            await cenarios.Uow.SaveChangesAsync(Ct);
            return apolice.Id;
        });

    private sealed class BusFalho : IMessageBus
    {
        public Task PublishAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("broker fora do ar");
    }
}

// Despacho imediato ligado (como na API): o SaveChanges publica logo após o commit e marca a mensagem.
public sealed class OutboxDispatchImediatoTests(PostgresFixture postgres) : TesteComBanco(postgres, "imediato")
{
    private readonly BusCapturador _bus = new();

    [Fact]
    public async Task Deve_publicar_e_marcar_processada_logo_apos_o_commit_quando_o_dispatch_imediato_esta_ligado()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        using var atividade = new Activity("teste.imediato").Start();

        await NoEscopoAsync(async sp =>
        {
            sp.GetRequiredService<ContextoCorrelacao>().Definir("corr-imediato");
            var cenarios = new Cenarios(sp);
            await cenarios.ApoliceAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            await cenarios.Uow.SaveChangesAsync(Ct);
        });

        var publicada = _bus.Publicadas.ShouldHaveSingleItem();
        publicada.Tipo.ShouldBe(nameof(ApoliceEmitida));
        publicada.CorrelationId.ShouldBe("corr-imediato");
        publicada.Traceparent.ShouldBe(atividade.Id);
        var mensagem = await Ambiente.ExecutarAsync<ApoliceDbContext, OutboxMessage>(db => db.Outbox.AsNoTracking().SingleAsync(Ct));
        mensagem.Id.ShouldBe(publicada.Id);
        mensagem.ProcessadoEm.ShouldNotBeNull();
    }

    [Fact]
    public async Task Deve_manter_pendente_sem_derrubar_o_save_quando_a_publicacao_imediata_falha()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        _bus.Falhar = true;

        await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            await cenarios.ApoliceAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            (await cenarios.Uow.SaveChangesAsync(Ct)).ShouldBeGreaterThan(0);
        });

        var pendentes = await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Outbox.CountAsync(m => m.ProcessadoEm == null, Ct));
        pendentes.ShouldBe(1);
    }

    protected override Dictionary<string, string?> Configuracao() => new() { ["Outbox:DispatchImediato"] = "true" };

    protected override void Servicos(IServiceCollection servicos) => servicos.AddSingleton<IMessageBus>(_bus);

    private sealed class BusCapturador : IMessageBus
    {
        public ConcurrentQueue<MensagemIntegracao> Publicadas { get; } = new();

        public bool Falhar { get; set; }

        public Task PublishAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken)
        {
            if (Falhar)
            {
                throw new InvalidOperationException("broker fora do ar");
            }

            Publicadas.Enqueue(mensagem);
            return Task.CompletedTask;
        }
    }
}

public sealed class NumeradorTests(PostgresFixture postgres) : TesteComBanco(postgres, "numerador")
{
    [Fact]
    public async Task Deve_entregar_sequenciais_sem_repetir_quando_chamado_em_paralelo()
    {
        const int Chamadas = 40;
        var numerador = Ambiente.Provedor.GetRequiredService<INumerador>();

        var numeros = await Task.WhenAll(Enumerable.Range(0, Chamadas).Select(_ => numerador.ProximoAsync("APOLICE:AUTO:2026", Ct)));

        numeros.Order().ShouldBe(Enumerable.Range(1, Chamadas).Select(n => (long)n));
        (await numerador.ProximoAsync("SINISTRO:2026", Ct)).ShouldBe(1);
        (await numerador.ProximoAsync("APOLICE:AUTO:2026", Ct)).ShouldBe(Chamadas + 1);
    }
}

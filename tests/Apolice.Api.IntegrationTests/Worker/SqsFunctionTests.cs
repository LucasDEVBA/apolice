using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Api.IntegrationTests.Worker.Suporte;
using Apolice.Domain.Notificacoes;
using Apolice.Infrastructure.Outbox;
using Apolice.Worker.Lambda;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Apolice.Api.IntegrationTests.Worker;

// Handler da Lambda consumidora contra o PostgreSQL real, com o host montado pela composição do Worker.
public sealed class SqsFunctionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = HostWorker.Criar(await postgres.CriarBancoAsync("worker_sqs"));
        await HostWorker.PrepararBancoAsync(_host.Services, Ct);
        await _host.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        await HostWorker.DescartarAsync(_host);
    }

    [Fact]
    public async Task Deve_processar_e_gerar_a_notificacao_sem_falhas_quando_a_mensagem_e_valida()
    {
        var emissao = await HostWorker.EmitirAsync(_host.Services, Ct);
        var mensagem = await MensagemDoOutboxAsync(emissao);

        var resposta = await new SqsFunction(_host.Services).HandleAsync(EventosSqs.Lote(EventosSqs.De(mensagem)), ContextoLambdaTeste.Folgado());

        resposta.BatchItemFailures.ShouldBeEmpty();
        var notificacao = await HostWorker.ConsultarAsync(_host.Services, db => db.Notificacoes.AsNoTracking().SingleAsync(n => n.ReferenciaId == emissao.ApoliceId, Ct));
        notificacao.Tipo.ShouldBe(TipoNotificacao.BoasVindas);
        (await HostWorker.ConsultarAsync(_host.Services, db => db.MensagensProcessadas.CountAsync(m => m.MensagemId == mensagem.Id, Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task Deve_devolver_so_os_itens_desconhecido_e_malformado_quando_o_lote_e_misto()
    {
        var emissao = await HostWorker.EmitirAsync(_host.Services, Ct);
        var valida = EventosSqs.De(await MensagemDoOutboxAsync(emissao));
        var desconhecida = EventosSqs.Registro(Guid.CreateVersion7(), "EventoQueNaoExiste", "{}");
        var malformada = EventosSqs.SemEnvelope("{}");

        var resposta = await new SqsFunction(_host.Services).HandleAsync(EventosSqs.Lote(desconhecida, valida, malformada), ContextoLambdaTeste.Folgado());

        resposta.BatchItemFailures.Select(f => f.ItemIdentifier).ShouldBe([desconhecida.MessageId, malformada.MessageId], ignoreOrder: true);
        (await HostWorker.ConsultarAsync(_host.Services, db => db.Notificacoes.CountAsync(n => n.ReferenciaId == emissao.ApoliceId, Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task Deve_gerar_uma_unica_notificacao_quando_a_mesma_mensagem_e_reentregue()
    {
        var emissao = await HostWorker.EmitirAsync(_host.Services, Ct);
        var mensagem = await MensagemDoOutboxAsync(emissao);
        var funcao = new SqsFunction(_host.Services);

        var primeira = await funcao.HandleAsync(EventosSqs.Lote(EventosSqs.De(mensagem)), ContextoLambdaTeste.Folgado());
        var reentrega = await funcao.HandleAsync(EventosSqs.Lote(EventosSqs.De(mensagem), EventosSqs.De(mensagem)), ContextoLambdaTeste.Folgado());

        primeira.BatchItemFailures.ShouldBeEmpty();
        reentrega.BatchItemFailures.ShouldBeEmpty();
        (await HostWorker.ConsultarAsync(_host.Services, db => db.Notificacoes.CountAsync(n => n.ReferenciaId == emissao.ApoliceId, Ct))).ShouldBe(1);
        (await HostWorker.ConsultarAsync(_host.Services, db => db.MensagensProcessadas.CountAsync(m => m.MensagemId == mensagem.Id, Ct))).ShouldBe(1);
    }

    [Fact]
    public async Task Deve_devolver_o_lote_inteiro_sem_processar_quando_o_prazo_da_invocacao_esta_no_fim()
    {
        var emissao = await HostWorker.EmitirAsync(_host.Services, Ct);
        var registro = EventosSqs.De(await MensagemDoOutboxAsync(emissao));

        var resposta = await new SqsFunction(_host.Services).HandleAsync(EventosSqs.Lote(registro), new ContextoLambdaTeste(TimeSpan.FromSeconds(1)));

        resposta.BatchItemFailures.ShouldHaveSingleItem().ItemIdentifier.ShouldBe(registro.MessageId);
        (await HostWorker.ConsultarAsync(_host.Services, db => db.Notificacoes.CountAsync(n => n.ReferenciaId == emissao.ApoliceId, Ct))).ShouldBe(0);
    }

    private async Task<OutboxMessage> MensagemDoOutboxAsync(Emissao emissao) =>
        await HostWorker.ConsultarAsync(_host.Services, db => db.Outbox.AsNoTracking().SingleAsync(m => m.Id == emissao.EventoId, Ct));
}

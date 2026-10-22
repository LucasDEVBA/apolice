using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices.Cancelar;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Eventos;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.IniciarAnalise;
using Apolice.Application.Features.Sinistros.Negar;
using Apolice.Application.Features.Sinistros.RegistrarPagamento;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Eventos;

public sealed class ConsumidoresTests : TesteBase
{
    [Fact]
    public async Task Deve_gerar_boas_vindas_formatadas_e_marcar_envio_quando_apolice_emitida()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromMinutes(2));

        await Amb.ProcessarOutboxAsync();

        var notificacao = Amb.Banco.Notificacoes.ShouldHaveSingleItem();
        notificacao.Tipo.ShouldBe(TipoNotificacao.BoasVindas);
        notificacao.Destinatario.Valor.ShouldBe(Dados.EmailSegurado);
        notificacao.Assunto.ShouldBe("Bem-vindo(a)! Sua apólice AUTO-2026-000001 foi emitida");
        notificacao.Corpo.ShouldStartWith("Olá, Maria Silva!");
        notificacao.Corpo.ShouldContain("(Seguro Auto)");
        notificacao.Corpo.ShouldContain("Vigência: de 10/03/2026 a 10/03/2027.");
        notificacao.Corpo.ShouldContain("Prêmio total: R$ 4.080,44.");
        notificacao.Corpo.ShouldEndWith("Atenciosamente,\nEquipe Apólice");
        Amb.Banco.Apolices.Single().BoasVindasEnviadasEm.ShouldBe(Amb.Agora);
        apolice.BoasVindasEnviadasEm.ShouldBeNull();
    }

    [Fact]
    public async Task Deve_nao_reenviar_boas_vindas_quando_evento_chega_com_outro_id()
    {
        await Amb.EmitirApoliceAsync(Amb.Ana);
        var original = Amb.Banco.Outbox.OfType<ApoliceEmitida>().Single();
        await Amb.ProcessarOutboxAsync();
        var copia = original with { };
        Amb.Avancar(TimeSpan.FromSeconds(1));
        var reemitido = new ApoliceEmitida(copia.ApoliceId, copia.Numero, copia.CorretorId, copia.ProdutoCodigo, copia.SeguradoNome, copia.SeguradoEmail, copia.PremioTotal, copia.VigenciaInicio, copia.VigenciaFim, Amb.Agora);

        await Amb.Servico<BoasVindasApoliceEmitidaHandler>().HandleAsync(reemitido, Ct);

        Amb.Banco.PendentesNaoSalvos.ShouldBe(0);
        Amb.Banco.Notificacoes.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Deve_usar_o_codigo_do_produto_quando_catalogo_nao_tem_o_produto()
    {
        await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Banco.Produtos.RemoveAll(p => p.Codigo == CodigosProduto.Auto);
        await Amb.Catalogo.InvalidarAsync(Ct);

        await Amb.ProcessarOutboxAsync();

        Amb.Banco.Notificacoes.Single().Corpo.ShouldContain("(AUTO)");
    }

    [Fact]
    public async Task Deve_notificar_endosso_com_nome_da_cobertura_data_de_efeito_e_ajuste_quando_apolice_endossada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.ProcessarOutboxAsync();
        Amb.Avancar(TimeSpan.FromDays(73));
        await Amb.Servico<EndossarApoliceHandler>().HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ct);

        await Amb.ProcessarOutboxAsync();

        var notificacao = Amb.Banco.Notificacoes.Single(n => n.Tipo == TipoNotificacao.Endosso);
        notificacao.Assunto.ShouldBe("Endosso nº 1 da apólice AUTO-2026-000001");
        notificacao.Corpo.ShouldContain("inclusão da cobertura Vidros, faróis e retrovisores, com efeito a partir de 22/05/2026.");
        notificacao.Corpo.ShouldContain("Ajuste de prêmio a pagar: R$ 188,99.");
    }

    // O evento chegou, mas o endosso não está na apólice (dado inconsistente): a notificação sai com o código e sem data,
    // em vez de travar a fila por um detalhe de texto.
    [Fact]
    public async Task Deve_notificar_com_o_codigo_e_sem_data_quando_o_endosso_do_evento_nao_existe()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.ProcessarOutboxAsync();
        var evento = new ApoliceEndossada(apolice.Id, apolice.Numero, 9, TipoEndosso.ExclusaoCobertura, CodigosCobertura.Vidros, -50m, "Maria Silva", Dados.EmailSegurado, Amb.Agora);

        await Amb.EntregarAsync(evento);

        var notificacao = Amb.Banco.Notificacoes.Single(n => n.Tipo == TipoNotificacao.Endosso);
        notificacao.ReferenciaId.ShouldBe(apolice.Id);
        notificacao.Corpo.ShouldContain("exclusão da cobertura VIDROS.");
        notificacao.Corpo.ShouldNotContain("com efeito a partir de");
        notificacao.Corpo.ShouldContain("Valor a restituir: R$ 50,00.");
    }

    [Fact]
    public async Task Deve_notificar_cancelamento_com_valor_a_restituir_quando_apolice_cancelada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(73));
        await Amb.Servico<CancelarApoliceHandler>().HandleAsync(new CancelarApoliceCommand(apolice.Id, MotivoCancelamento.Inadimplencia), Ct);

        await Amb.ProcessarOutboxAsync();

        var notificacao = Amb.Banco.Notificacoes.Single(n => n.Tipo == TipoNotificacao.Cancelamento);
        notificacao.ReferenciaId.ShouldBe(apolice.Id);
        notificacao.Corpo.ShouldContain("por inadimplência.");
        notificacao.Corpo.ShouldContain("Valor a restituir: R$ 3.040,00 (o IOF não é restituível).");
    }

    [Fact]
    public async Task Deve_triar_e_notificar_quando_sinistro_aberto_com_flag_ligada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(40));
        var sinistro = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id, 90_000m, Amb.Hoje.AddDays(-35));

        await Amb.ProcessarOutboxAsync();

        // Ocorrência 5 dias após o início (+40), reclamado > 80% do LMI (+30), aviso 35 dias depois (+10).
        var triagem = Amb.Banco.Sinistros.Single().Triagem.ShouldNotBeNull();
        triagem.Score.ShouldBe(80);
        triagem.Alertas.ShouldBe([AlertasTriagem.VigenciaRecente, AlertasTriagem.ValorProximoLmi, AlertasTriagem.ComunicacaoTardia]);
        triagem.TriadoEm.ShouldBe(Amb.Agora);
        Amb.Auditoria.Registros.Last(r => r.Acao == AcoesAuditoria.SinistroTriado).Detalhes
            .DeveSerJsonEquivalente("""{"score":80,"alertas":["VIGENCIA_RECENTE","VALOR_PROXIMO_LMI","COMUNICACAO_TARDIA"]}""");
        var notificacao = Amb.Banco.Notificacoes.Single(n => n.Tipo == TipoNotificacao.SinistroAberto);
        notificacao.ReferenciaId.ShouldBe(sinistro.Id);
        notificacao.Corpo.ShouldContain("ocorrido em 15/03/2026");
        notificacao.Corpo.ShouldContain("Valor reclamado: R$ 90.000,00.");
    }

    [Fact]
    public async Task Deve_notificar_sem_triar_quando_flag_de_triagem_desligada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        Amb.Flags.Desligar(FeatureFlags.TriagemAutomaticaSinistros);

        await Amb.ProcessarOutboxAsync();

        Amb.Banco.Sinistros.Single().Triagem.ShouldBeNull();
        Amb.Banco.Notificacoes.ShouldContain(n => n.Tipo == TipoNotificacao.SinistroAberto);
    }

    [Fact]
    public async Task Deve_nao_triar_de_novo_quando_sinistro_ja_tem_triagem()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        var evento = Amb.Banco.Outbox.OfType<SinistroAberto>().Single();
        await Amb.ProcessarOutboxAsync();
        var triagens = Amb.Auditoria.Acoes.Count(a => a == AcoesAuditoria.SinistroTriado);

        await Amb.Servico<TriarSinistroAbertoHandler>().HandleAsync(evento, Ct);

        Amb.Auditoria.Acoes.Count(a => a == AcoesAuditoria.SinistroTriado).ShouldBe(triagens);
    }

    [Fact]
    public async Task Deve_notificar_cada_decisao_quando_sinistro_aprovado_pago_ou_negado()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        var aprovado = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        var negado = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id, 2_000m);
        Amb.Como(Amb.Carla);
        await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(aprovado.Id), Ct);
        await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(aprovado.Id, 6_543.21m, null), Ct);
        await Amb.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(aprovado.Id), Ct);
        await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(negado.Id), Ct);
        await Amb.Servico<NegarSinistroHandler>().HandleAsync(new NegarSinistroCommand(negado.Id, "Evento não coberto pelas condições gerais."), Ct);

        await Amb.ProcessarOutboxAsync();

        var decisoes = Amb.Banco.Notificacoes.Where(n => n.Tipo == TipoNotificacao.SinistroDecidido).ToList();
        decisoes.Count.ShouldBe(3);
        decisoes.ShouldContain(n => n.Assunto == $"Sinistro {aprovado.Numero} aprovado" && n.Corpo.Contains("Valor da indenização: R$ 6.543,21.", StringComparison.Ordinal));
        decisoes.ShouldContain(n => n.Assunto == $"Pagamento do sinistro {aprovado.Numero}" && n.Corpo.Contains("Valor pago: R$ 6.543,21.", StringComparison.Ordinal));
        decisoes.ShouldContain(n => n.Assunto == $"Sinistro {negado.Numero}: resultado da análise" && n.Corpo.Contains("Motivo: Evento não coberto pelas condições gerais.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deve_lancar_quando_agregado_do_evento_nao_existe()
    {
        var ausente = Guid.NewGuid();
        var agora = Amb.Agora;

        await Should.ThrowAsync<InvalidOperationException>(() => Amb.Servico<BoasVindasApoliceEmitidaHandler>()
            .HandleAsync(new ApoliceEmitida(ausente, "AUTO-2026-000009", Guid.NewGuid(), "AUTO", "Maria", Dados.EmailSegurado, 10m, Amb.Hoje, Amb.Hoje.AddYears(1), agora), Ct));
        await Should.ThrowAsync<InvalidOperationException>(() => Amb.Servico<NotificarApoliceEndossadaHandler>()
            .HandleAsync(new ApoliceEndossada(ausente, "AUTO-2026-000009", 1, TipoEndosso.AlteracaoLmi, "CASCO", 0m, "Maria", Dados.EmailSegurado, agora), Ct));
        await Should.ThrowAsync<InvalidOperationException>(() => Amb.Servico<NotificarSinistroPagoHandler>()
            .HandleAsync(new SinistroPago(ausente, "SIN-2026-000009", Dados.EmailSegurado, agora), Ct));
    }

    [Fact]
    public async Task Deve_lancar_quando_evento_traz_email_invalido()
    {
        var evento = new ApoliceCancelada(Guid.NewGuid(), "AUTO-2026-000009", MotivoCancelamento.PedidoDoSegurado, 0m, "Maria", "sem-arroba", Amb.Agora);

        await Should.ThrowAsync<InvalidOperationException>(() => Amb.Servico<NotificarApoliceCanceladaHandler>().HandleAsync(evento, Ct));
    }

    [Fact]
    public async Task Deve_lancar_quando_modelo_gera_texto_invalido_para_o_dominio()
    {
        var evento = new ApoliceCancelada(Guid.NewGuid(), new string('9', 250), MotivoCancelamento.PedidoDoSegurado, 0m, "Maria", Dados.EmailSegurado, Amb.Agora);

        await Should.ThrowAsync<InvalidOperationException>(() => Amb.Servico<IIntegrationEventHandler<ApoliceCancelada>>().HandleAsync(evento, Ct));
    }
}

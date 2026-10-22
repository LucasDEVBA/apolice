using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;

namespace Apolice.Application.Tests.Apolices;

public sealed class EmitirApoliceHandlerTests : TesteBase
{
    private EmitirApoliceHandler Handler => Amb.Servico<EmitirApoliceHandler>();

    [Fact]
    public async Task Deve_emitir_numerar_auditar_e_devolver_detalhe_quando_cotacao_aceita()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);

        var resultado = await Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 3), Ct);

        var apolice = resultado.DeveTerSucesso();
        apolice.Numero.ShouldBe("AUTO-2026-000001");
        apolice.Status.ShouldBe(StatusEfetivoApolice.Ativa);
        apolice.VigenciaInicio.ShouldBe(new DateOnly(2026, 3, 10));
        apolice.VigenciaFim.ShouldBe(new DateOnly(2027, 3, 10));
        apolice.CorretorNome.ShouldBe("Ana Ribeiro");
        apolice.ProdutoNome.ShouldBe("Seguro Auto");
        apolice.PremioTotal.ShouldBe(Dados.PremioTotalAuto);
        apolice.PremioLiquidoVigente.ShouldBe(Dados.PremioLiquidoAuto);
        apolice.Coberturas.Count.ShouldBe(2);
        apolice.Endossos.ShouldBeEmpty();
        apolice.Sinistros.ShouldBeEmpty();
        apolice.Notificacoes.ShouldBeEmpty();
        apolice.BoasVindasEnviadasEm.ShouldBeNull();
        Amb.Banco.Numeradores["APOLICE:AUTO:2026"].ShouldBe(1);
        Amb.Banco.Cotacoes.Single().Status.ShouldBe(StatusCotacao.Emitida);
        Amb.Banco.Cotacoes.Single().ApoliceId.ShouldBe(apolice.Id);
        Amb.Auditoria.Acoes.Last().ShouldBe(AcoesAuditoria.ApoliceEmitida);
        Amb.Banco.Outbox.OfType<ApoliceEmitida>().ShouldHaveSingleItem().Numero.ShouldBe(apolice.Numero);
    }

    [Fact]
    public async Task Deve_vir_o_cronograma_da_regra_do_dominio_quando_parcelado()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana, parcelas: 3);

        // 4.080,44 ÷ 3 = 1.360,146… → base truncada 1.360,14; a 1ª absorve a sobra (1.360,16).
        apolice.CronogramaParcelas.ShouldBe(
        [
            new ParcelaDto(1, new DateOnly(2026, 3, 10), 1_360.16m),
            new ParcelaDto(2, new DateOnly(2026, 4, 10), 1_360.14m),
            new ParcelaDto(3, new DateOnly(2026, 5, 10), 1_360.14m),
        ]);
        apolice.CronogramaParcelas.Sum(p => p.Valor).ShouldBe(apolice.PremioTotal);
    }

    [Fact]
    public async Task Deve_seguir_a_sequencia_por_produto_e_ano_quando_emite_varias()
    {
        await Amb.EmitirApoliceAsync(Amb.Ana);
        var segunda = await Amb.EmitirApoliceAsync(Amb.Bruno);
        var vida = await Amb.EmitirApoliceAsync(Amb.Ana, request: Dados.CotacaoVida());

        segunda.Numero.ShouldBe("AUTO-2026-000002");
        vida.Numero.ShouldBe("VIDA-2026-000001");
    }

    [Fact]
    public async Task Deve_nao_consumir_numero_quando_cotacao_nao_esta_aceita_ou_expirou()
    {
        var calculada = await Amb.CriarCotacaoAsync(Amb.Ana);
        var aceita = await Amb.CotacaoAceitaAsync(Amb.Ana);

        var naoAceita = await Handler.HandleAsync(new EmitirApoliceCommand(calculada.Id, Amb.Hoje, 1), Ct);
        Amb.Avancar(TimeSpan.FromDays(31));
        var expirada = await Handler.HandleAsync(new EmitirApoliceCommand(aceita.Id, Amb.Hoje, 1), Ct);

        naoAceita.DeveFalharCom(CotacaoErros.StatusInvalido(StatusCotacao.Calculada).Codigo, ErrorType.RegraDeNegocio);
        expirada.DeveFalharCom(CotacaoErros.Expirada.Codigo, ErrorType.RegraDeNegocio);
        Amb.Banco.Numeradores.ShouldBeEmpty();
        Amb.Banco.Apolices.ShouldBeEmpty();
    }

    // Duplo clique ou retry sem Idempotency-Key: a segunda emissão é recusada antes de numerar e não gera evento.
    [Fact]
    public async Task Deve_recusar_sem_gastar_numero_quando_reemite_cotacao_ja_emitida()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);
        (await Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 1), Ct)).DeveTerSucesso();
        var savesAntes = Amb.Banco.TotalSaves;

        var segunda = await Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 1), Ct);

        segunda.DeveFalharCom(CotacaoErros.StatusInvalido(StatusCotacao.Emitida).Codigo, ErrorType.RegraDeNegocio);
        Amb.Banco.Numeradores["APOLICE:AUTO:2026"].ShouldBe(1);
        Amb.Banco.Apolices.ShouldHaveSingleItem();
        Amb.Banco.Outbox.OfType<ApoliceEmitida>().ShouldHaveSingleItem();
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
    }

    // Duas emissões da mesma cotação ao mesmo tempo: a que perde a disputa de versão não grava nada (o número já
    // gasto vira lacuna, aceita pelo SPEC 6) e o 409 chega ao cliente.
    [Fact]
    public async Task Deve_propagar_o_conflito_sem_gravar_quando_a_cotacao_muda_durante_a_emissao()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);
        Amb.Banco.AgendarEscritaConcorrente(Amb.Banco.Cotacoes.Single(), c => c.Recusar(Amb.Agora));

        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() => Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 1), Ct));

        Amb.Banco.Apolices.ShouldBeEmpty();
        Amb.Banco.Outbox.OfType<ApoliceEmitida>().ShouldBeEmpty();
        Amb.Banco.Cotacoes.Single().Status.ShouldBe(StatusCotacao.Recusada);
        Amb.Banco.Numeradores["APOLICE:AUTO:2026"].ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 11, "Apolice.ParcelasInvalidas")]
    [InlineData(31, 1, "Apolice.InicioVigenciaInvalido")]
    [InlineData(-1, 1, "Apolice.InicioVigenciaInvalido")]
    public async Task Deve_retornar_erro_do_dominio_e_aceitar_lacuna_quando_dados_de_emissao_invalidos(int diasAteInicio, int parcelas, string codigo)
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);

        var resultado = await Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje.AddDays(diasAteInicio), parcelas), Ct);

        resultado.DeveFalharCom(codigo, ErrorType.Validacao);
        Amb.Banco.Apolices.ShouldBeEmpty();
        Amb.Banco.Numeradores["APOLICE:AUTO:2026"].ShouldBe(1);
    }

    [Fact]
    public async Task Deve_proibir_analista_e_permitir_admin_quando_emitem_cotacao_de_corretor()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);

        Amb.Como(Amb.Carla);
        var analista = await Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 1), Ct);
        Amb.Como(Amb.Diego);
        var admin = await Handler.HandleAsync(new EmitirApoliceCommand(cotacao.Id, Amb.Hoje, 1), Ct);

        analista.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        admin.DeveTerSucesso().CorretorId.ShouldBe(Amb.Ana.Id);
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_antes_de_procurar_a_cotacao_quando_anonimo()
    {
        Amb.UsuarioAtual.Anonimo();

        var resultado = await Handler.HandleAsync(new EmitirApoliceCommand(Guid.NewGuid(), Amb.Hoje, 1), Ct);

        resultado.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }

    [Fact]
    public async Task Deve_retornar_nao_encontrado_quando_cotacao_inexistente()
    {
        var resultado = await Handler.HandleAsync(new EmitirApoliceCommand(Guid.NewGuid(), Amb.Hoje, 1), Ct);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
    }
}

using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

public sealed class PersistenciaTests(PostgresFixture postgres) : TesteComBanco(postgres, "persistencia")
{
    [Fact]
    public async Task Deve_reler_a_cotacao_completa_e_na_mesma_ordem_quando_gravar()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var original = await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            var cotacao = await cenarios.CotacaoAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            await cenarios.Uow.SaveChangesAsync(Ct);
            return cotacao;
        });

        var relida = await NoEscopoAsync(sp => sp.GetRequiredService<ICotacaoRepository>().ObterAsync(original.Id, Ct));

        relida.ShouldNotBeNull();
        relida.Itens.ShouldBe(original.Itens);
        relida.Fatores.ShouldBe(original.Fatores);
        relida.Fatores.Count.ShouldBe(4);
        relida.Segurado.ShouldBe(original.Segurado);
        relida.DadosRisco.ShouldBe(original.DadosRisco);
        relida.PremioTotal.ShouldBe(original.PremioTotal);
        relida.AliquotaIof.ShouldBe(0.0738m);
        relida.CriadaEm.ShouldBe(original.CriadaEm, TimeSpan.FromMilliseconds(1));
        relida.Versao.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_incrementar_a_versao_da_raiz_quando_so_os_filhos_mudam()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var apoliceId = await EmitirAsync(ana);

        await NoEscopoAsync(async sp =>
        {
            var apolice = (await sp.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct))!;
            var produto = await new Cenarios(sp).ProdutoAsync(CodigosProduto.Auto);
            var endosso = apolice.Endossar(new SolicitacaoEndosso(TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros), produto, sp.GetRequiredService<Domain.Tarifacao.ITarifador>(), ana.Id, Ambiente.Tempo.GetUtcNow());
            endosso.IsSuccess.ShouldBeTrue();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var relida = await NoEscopoAsync(sp => sp.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct));
        relida!.Versao.ShouldBe(1);
        relida.Endossos.Count.ShouldBe(1);
        relida.Coberturas[^1].Codigo.ShouldBe(CodigosCobertura.Vidros);
        relida.Coberturas.Select(c => c.Codigo).ShouldBe([CodigosCobertura.Casco, CodigosCobertura.RcfDanosMateriais, CodigosCobertura.RcfDanosCorporais, CodigosCobertura.Vidros]);
    }

    [Fact]
    public async Task Deve_lancar_conflito_quando_outra_operacao_gravou_a_apolice_antes()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var apoliceId = await EmitirAsync(ana);
        await using var escopoA = Ambiente.Provedor.CreateAsyncScope();
        await using var escopoB = Ambiente.Provedor.CreateAsyncScope();
        var apoliceA = (await escopoA.ServiceProvider.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct))!;
        var apoliceB = (await escopoB.ServiceProvider.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct))!;
        var agora = Ambiente.Tempo.GetUtcNow();

        var produto = await new Cenarios(escopoA.ServiceProvider).ProdutoAsync(CodigosProduto.Auto);
        apoliceA.Endossar(new SolicitacaoEndosso(TipoEndosso.ExclusaoCobertura, CodigosCobertura.RcfDanosCorporais), produto, escopoA.ServiceProvider.GetRequiredService<Domain.Tarifacao.ITarifador>(), ana.Id, agora).IsSuccess.ShouldBeTrue();
        await escopoA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        apoliceB.Cancelar(MotivoCancelamento.PedidoDoSegurado, agora).IsSuccess.ShouldBeTrue();
        var uowB = escopoB.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await Should.ThrowAsync<ConflitoDeConcorrenciaException>(() => uowB.SaveChangesAsync(Ct));

        // Refazer sobre o estado gravado: descartar, reler e repetir funciona.
        uowB.DescartarAlteracoes();
        var recarregada = (await escopoB.ServiceProvider.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct))!;
        recarregada.Versao.ShouldBe(1);
        recarregada.Coberturas.ShouldNotContain(c => c.Codigo == CodigosCobertura.RcfDanosCorporais);
        recarregada.Cancelar(MotivoCancelamento.PedidoDoSegurado, agora).IsSuccess.ShouldBeTrue();
        await uowB.SaveChangesAsync(Ct);

        var final = await Ambiente.ExecutarAsync<ApoliceDbContext, ApoliceSeguro>(db => db.Apolices.AsNoTracking().SingleAsync(a => a.Id == apoliceId, Ct));
        final.Status.ShouldBe(StatusApolice.Cancelada);
        final.Versao.ShouldBe(2);
        (await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Set<CoberturaApolice>().CountAsync(c => EF.Property<Guid>(c, "ApoliceId") == apoliceId, Ct))).ShouldBe(2);
    }

    [Fact]
    public async Task Deve_persistir_historico_em_ordem_e_triagem_quando_o_sinistro_avanca()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var carla = await UsuarioAsync(PersonasDemo.Analista);
        var apoliceId = await EmitirAsync(ana);
        Ambiente.Tempo.Advance(TimeSpan.FromDays(3));
        var sinistroId = await NoEscopoAsync(async sp =>
        {
            var apolice = (await sp.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct))!;
            var sinistro = await new Cenarios(sp).AbrirSinistroAsync(apolice, ana.Id, Ambiente.Tempo.GetUtcNow());
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            return sinistro.Id;
        });

        await NoEscopoAsync(async sp =>
        {
            var sinistro = (await sp.GetRequiredService<ISinistroRepository>().ObterAsync(sinistroId, Ct))!;
            var agora = Ambiente.Tempo.GetUtcNow();
            sinistro.RegistrarTriagem(TriagemSinistro.Criar(40, [AlertasTriagem.VigenciaRecente], agora).Value, agora);
            sinistro.IniciarAnalise(carla.Id, agora.AddHours(1)).IsSuccess.ShouldBeTrue();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var relido = (await NoEscopoAsync(sp => sp.GetRequiredService<ISinistroRepository>().ObterAsync(sinistroId, Ct)))!;
        relido.Historico.Select(h => h.Status).ShouldBe([StatusSinistro.Aberto, StatusSinistro.EmAnalise]);
        relido.Historico[1].UsuarioId.ShouldBe(carla.Id);
        relido.Triagem.ShouldNotBeNull();
        relido.Triagem.Score.ShouldBe(40);
        relido.Triagem.Alertas.ShouldBe([AlertasTriagem.VigenciaRecente]);
        relido.SeguradoEmail.Valor.ShouldBe("maria.silva@exemplo.test");
        relido.Versao.ShouldBe(1);
    }

    // O Npgsql só grava timestamptz com offset zero: o conversor das convenções cobre também os anuláveis.
    [Fact]
    public async Task Deve_gravar_instantes_anulaveis_com_offset_de_brasilia_convertendo_para_utc()
    {
        var bruno = await UsuarioAsync(PersonasDemo.Corretor);
        var emBrasilia = Ambiente.Tempo.GetUtcNow().ToOffset(Relogio.OffsetBrasilia);

        await NoEscopoAsync(async sp =>
        {
            var usuario = (await sp.GetRequiredService<IUsuarioRepository>().ObterAsync(bruno.Id, Ct))!;
            for (var i = 0; i < 5; i++)
            {
                usuario.RegistrarFalhaLogin(emBrasilia);
            }

            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var relido = (await NoEscopoAsync(sp => sp.GetRequiredService<IUsuarioRepository>().ObterAsync(bruno.Id, Ct)))!;
        relido.BloqueadoAte.ShouldBe(emBrasilia.AddMinutes(15));
        relido.BloqueadoAte!.Value.Offset.ShouldBe(TimeSpan.Zero);
        relido.Versao.ShouldBe(1);
    }

    [Fact]
    public async Task Deve_achar_usuario_pelo_email_normalizado_e_listar_so_tokens_ativos_da_familia()
    {
        var bruno = await UsuarioAsync(PersonasDemo.Corretor);
        var agora = Ambiente.Tempo.GetUtcNow();
        var familia = Guid.CreateVersion7();
        await NoEscopoAsync(async sp =>
        {
            var tokens = sp.GetRequiredService<IRefreshTokenRepository>();
            var ativo = RefreshToken.Criar(bruno.Id, RefreshToken.CalcularHash("token-ativo"), familia, agora, TimeSpan.FromDays(7));
            var revogado = RefreshToken.Criar(bruno.Id, RefreshToken.CalcularHash("token-revogado"), familia, agora, TimeSpan.FromDays(7));
            revogado.Revogar(agora);
            tokens.Adicionar(ativo);
            tokens.Adicionar(revogado);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var usuario = await NoEscopoAsync(sp => sp.GetRequiredService<IUsuarioRepository>().ObterPorEmailAsync(Email.Criar("  BRUNO.Corretor@Apolice.dev ").Value, Ct));
        usuario!.Id.ShouldBe(bruno.Id);
        var ativos = await NoEscopoAsync(sp => sp.GetRequiredService<IRefreshTokenRepository>().ListarNaoRevogadosDaFamiliaAsync(familia, Ct));
        ativos.ShouldHaveSingleItem().TokenHash.ShouldBe(RefreshToken.CalcularHash("token-ativo"));
        var porHash = await NoEscopoAsync(sp => sp.GetRequiredService<IRefreshTokenRepository>().ObterPorHashAsync(RefreshToken.CalcularHash("token-revogado"), Ct));
        porHash!.EstaRevogado.ShouldBeTrue();
    }

    [Fact]
    public async Task Deve_descartar_agregado_e_auditoria_pendentes_quando_descartar_alteracoes()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        await NoEscopoAsync(async sp =>
        {
            await new Cenarios(sp).CotacaoAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow());
            sp.GetRequiredService<IAuditoria>().Registrar("teste.descartado", "Cotacao");
            var uow = sp.GetRequiredService<IUnitOfWork>();
            uow.DescartarAlteracoes();
            (await uow.SaveChangesAsync(Ct)).ShouldBe(0);
        });

        (await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Cotacoes.CountAsync(Ct))).ShouldBe(0);
        (await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Auditoria.CountAsync(a => a.Acao == "teste.descartado", Ct))).ShouldBe(0);
    }

    [Fact]
    public async Task Deve_considerar_registro_do_inbox_ainda_nao_salvo_quando_verificar_mensagem()
    {
        var mensagemId = Guid.CreateVersion7();
        await NoEscopoAsync(async sp =>
        {
            var inbox = sp.GetRequiredService<IMensagensProcessadas>();
            (await inbox.JaProcessadaAsync(mensagemId, "Consumidor", Ct)).ShouldBeFalse();
            inbox.Registrar(mensagemId, "Consumidor", Ambiente.Tempo.GetUtcNow());
            (await inbox.JaProcessadaAsync(mensagemId, "Consumidor", Ct)).ShouldBeTrue();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        (await NoEscopoAsync(sp => sp.GetRequiredService<IMensagensProcessadas>().JaProcessadaAsync(mensagemId, "Consumidor", Ct))).ShouldBeTrue();
        (await NoEscopoAsync(sp => sp.GetRequiredService<IMensagensProcessadas>().JaProcessadaAsync(mensagemId, "Outro", Ct))).ShouldBeFalse();
    }

    private async Task<Guid> EmitirAsync(Usuario corretor) =>
        await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            var apolice = await cenarios.ApoliceAutoAsync(corretor.Id, Ambiente.Tempo.GetUtcNow());
            await cenarios.Uow.SaveChangesAsync(Ct);
            return apolice.Id;
        });
}

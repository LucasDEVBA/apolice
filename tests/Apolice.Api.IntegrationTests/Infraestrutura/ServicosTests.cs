using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Produtos.AtualizarTaxa;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Idempotencia;
using Apolice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

public sealed class ServicosTests(PostgresFixture postgres) : TesteComBanco(postgres, "servicos")
{
    [Fact]
    public async Task Deve_entregar_instancias_exclusivas_e_so_ver_a_taxa_nova_depois_de_invalidar()
    {
        var antes = await NoEscopoAsync(sp => sp.GetRequiredService<ICatalogoProdutos>().ListarAsync(Ct));
        antes.Select(p => p.Codigo).ShouldBe([CodigosProduto.Auto, CodigosProduto.Residencial, CodigosProduto.Vida]);
        antes[0].Coberturas.Select(c => c.Codigo).ShouldBe(CatalogoPadrao.Auto().Coberturas.Select(c => c.Codigo));

        var primeira = await NoEscopoAsync(sp => sp.GetRequiredService<ICatalogoProdutos>().ObterAsync(CodigosProduto.Auto, Ct));
        var segunda = await NoEscopoAsync(sp => sp.GetRequiredService<ICatalogoProdutos>().ObterAsync(CodigosProduto.Auto, Ct));
        ReferenceEquals(primeira, segunda).ShouldBeFalse();
        primeira!.AtualizarTaxa(CodigosCobertura.Casco, 0.05m, Ambiente.Tempo.GetUtcNow()).IsSuccess.ShouldBeTrue();
        segunda!.ObterCobertura(CodigosCobertura.Casco)!.Taxa.ShouldBe(0.0320m);

        // Gravação direta no banco sem invalidar: o cache segue com o retrato antigo (prova que há cache).
        await NoEscopoAsync(async sp =>
        {
            var produto = (await sp.GetRequiredService<IProdutoRepository>().ObterAsync(CodigosProduto.Auto, Ct))!;
            produto.AtualizarTaxa(CodigosCobertura.Casco, 0.0450m, Ambiente.Tempo.GetUtcNow());
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });
        (await TaxaCascoNoCatalogoAsync()).ShouldBe(0.0320m);

        await NoEscopoAsync(sp => sp.GetRequiredService<ICatalogoProdutos>().InvalidarAsync(Ct));
        (await TaxaCascoNoCatalogoAsync()).ShouldBe(0.0450m);
    }

    [Fact]
    public async Task Deve_invalidar_o_catalogo_quando_o_admin_atualiza_a_taxa_pelo_handler()
    {
        await TaxaCascoNoCatalogoAsync();
        Ambiente.Usuario.Definir(await UsuarioAsync(PersonasDemo.Admin));

        var resultado = await NoEscopoAsync(sp => sp.GetRequiredService<AtualizarTaxaCoberturaHandler>()
            .HandleAsync(new AtualizarTaxaCoberturaCommand("auto", "casco", 0.0333m), Ct));

        resultado.IsSuccess.ShouldBeTrue();
        (await TaxaCascoNoCatalogoAsync()).ShouldBe(0.0333m);
        var versao = await Ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Produtos.Where(p => p.Id == CodigosProduto.Auto).Select(p => p.Versao).SingleAsync(Ct));
        versao.ShouldBe(1);
    }

    [Fact]
    public async Task Deve_gravar_auditoria_com_usuario_e_correlacao_e_paginar_em_ordem_decrescente()
    {
        var admin = await UsuarioAsync(PersonasDemo.Admin);
        Ambiente.Usuario.Definir(admin);
        for (var i = 1; i <= 5; i++)
        {
            Ambiente.Tempo.Advance(TimeSpan.FromMinutes(1));
            var indice = i;
            await NoEscopoAsync(async sp =>
            {
                sp.GetRequiredService<ContextoCorrelacao>().Definir($"corr-{indice}");
                sp.GetRequiredService<IAuditoria>().Registrar("teste.acao", "Teste", indice.ToString(System.Globalization.CultureInfo.InvariantCulture), "{\"n\":1}");
                await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            });
        }

        var primeira = await NoEscopoAsync(sp => sp.GetRequiredService<IAuditoriaQueries>().ListarAsync(null, 2, Ct));
        primeira.Itens.Select(a => a.EntidadeId).ShouldBe(["5", "4"]);
        primeira.Itens[0].UsuarioNome.ShouldBe("Diego Martins");
        primeira.Itens[0].CorrelationId.ShouldBe("corr-5");
        primeira.Itens[0].Detalhes.ShouldBe("{\"n\":1}");
        primeira.ProximoCursor.ShouldNotBeNull();

        var segunda = await NoEscopoAsync(sp => sp.GetRequiredService<IAuditoriaQueries>().ListarAsync(Application.Common.Cursor.Decodificar(primeira.ProximoCursor).Value, 2, Ct));
        segunda.Itens.Select(a => a.EntidadeId).ShouldBe(["3", "2"]);
    }

    [Fact]
    public async Task Deve_reservar_a_chave_uma_unica_vez_e_devolver_a_resposta_concluida_enquanto_vale()
    {
        var armazem = Ambiente.Provedor.GetRequiredService<IArmazemIdempotencia>();
        var usuario = Guid.CreateVersion7();
        var agora = Ambiente.Tempo.GetUtcNow();
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var resposta = new RespostaIdempotente(hashA, 201, "application/json", "{\"id\":1}");

        (await armazem.ReservarAsync(usuario, "chave-1", hashA, agora, Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);
        var concorrente = await armazem.ReservarAsync(usuario, "chave-1", hashB, agora, Ct);
        concorrente.Estado.ShouldBe(EstadoReservaIdempotencia.EmProcessamento);
        concorrente.Existente!.HashRequisicao.ShouldBe(hashA);

        (await armazem.ConcluirAsync(usuario, "chave-1", resposta, agora, Ct)).ShouldBeTrue();
        (await armazem.ConcluirAsync(usuario, "chave-1", resposta with { Corpo = "{}" }, agora, Ct)).ShouldBeFalse();
        var repetida = await armazem.ReservarAsync(usuario, "chave-1", hashA, agora.AddHours(23), Ct);
        repetida.Estado.ShouldBe(EstadoReservaIdempotencia.Concluida);
        repetida.Existente.ShouldBe(resposta);
        (await armazem.ReservarAsync(Guid.CreateVersion7(), "chave-1", hashA, agora, Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);

        // Vencida (24 h): some da leitura, é purgada e a chave pode ser reutilizada.
        (await armazem.ObterAsync(usuario, "chave-1", agora.AddHours(25), Ct)).ShouldBeNull();
        (await armazem.PurgarVencidosAsync(agora.AddHours(25), 100, Ct)).ShouldBeGreaterThanOrEqualTo(1);
        (await armazem.ReservarAsync(usuario, "chave-1", hashB, agora.AddHours(25), Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);
    }

    [Fact]
    public async Task Deve_liberar_a_chave_quando_a_execucao_falha_e_quando_a_reserva_fica_orfa()
    {
        var armazem = Ambiente.Provedor.GetRequiredService<IArmazemIdempotencia>();
        var usuario = Guid.CreateVersion7();
        var agora = Ambiente.Tempo.GetUtcNow();
        var hash = new string('c', 64);

        (await armazem.ReservarAsync(usuario, "falhou", hash, agora, Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);
        await armazem.LiberarAsync(usuario, "falhou", Ct);
        (await armazem.ReservarAsync(usuario, "falhou", hash, agora, Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);

        (await armazem.ReservarAsync(usuario, "orfa", hash, agora, Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);
        (await armazem.ReservarAsync(usuario, "orfa", hash, agora.AddMinutes(1), Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.EmProcessamento);
        (await armazem.ReservarAsync(usuario, "orfa", hash, agora.AddMinutes(3), Ct)).Estado.ShouldBe(EstadoReservaIdempotencia.Reservada);
    }

    [Fact]
    public async Task Deve_ligar_todas_as_flags_por_padrao()
    {
        var flags = Ambiente.Provedor.GetRequiredService<IFeatureFlags>();
        foreach (var flag in new[] { FeatureFlags.LoginDemo, FeatureFlags.TriagemAutomaticaSinistros, FeatureFlags.DocumentoPdf, FeatureFlags.EndossoOnline })
        {
            (await flags.IsEnabledAsync(flag, Ct)).ShouldBeTrue(flag);
        }
    }

    [Fact]
    public async Task Deve_reportar_o_banco_saudavel_na_prontidao()
    {
        var relatorio = await Ambiente.Provedor.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Tags.Contains(Apolice.Infrastructure.DependencyInjection.TagProntidao), Ct);

        relatorio.Status.ShouldBe(HealthStatus.Healthy);
        relatorio.Entries.Keys.ShouldContain("banco");
    }

    [Fact]
    public async Task Deve_gerar_pdf_valido_para_apolice_cancelada_e_endossada()
    {
        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var apoliceId = await NoEscopoAsync(async sp =>
        {
            var cenarios = new Cenarios(sp);
            var apolice = await cenarios.ApoliceAutoAsync(ana.Id, Ambiente.Tempo.GetUtcNow(), Cenarios.Segurado("Otávio Brandão Simões"));
            await cenarios.Uow.SaveChangesAsync(Ct);
            return apolice.Id;
        });
        Ambiente.Tempo.Advance(TimeSpan.FromDays(40));
        await NoEscopoAsync(async sp =>
        {
            var apolice = (await sp.GetRequiredService<IApoliceRepository>().ObterAsync(apoliceId, Ct))!;
            var produto = await new Cenarios(sp).ProdutoAsync(CodigosProduto.Auto);
            var tarifador = sp.GetRequiredService<Domain.Tarifacao.ITarifador>();
            apolice.Endossar(new SolicitacaoEndosso(TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 250_000m), produto, tarifador, ana.Id, Ambiente.Tempo.GetUtcNow()).IsSuccess.ShouldBeTrue();
            apolice.Endossar(new SolicitacaoEndosso(TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros), produto, tarifador, ana.Id, Ambiente.Tempo.GetUtcNow()).IsSuccess.ShouldBeTrue();
            apolice.Cancelar(MotivoCancelamento.IniciativaSeguradora, Ambiente.Tempo.GetUtcNow().AddHours(2)).IsSuccess.ShouldBeTrue();
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var pdf = await NoEscopoAsync(async sp =>
        {
            var dto = (await sp.GetRequiredService<IApoliceQueries>().ObterAsync(apoliceId, null, Relogio.HojeEm(Ambiente.Tempo.GetUtcNow()), Ct))!;
            dto.Status.ShouldBe(StatusEfetivoApolice.Cancelada);
            return await sp.GetRequiredService<IGeradorDocumentoApolice>().GerarAsync(dto, Ct);
        });

        Documentos.DeveParecerPdf(pdf, "apolice-cancelada.pdf");
    }

    private async Task<decimal> TaxaCascoNoCatalogoAsync() =>
        (await NoEscopoAsync(sp => sp.GetRequiredService<ICatalogoProdutos>().ObterAsync(CodigosProduto.Auto, Ct)))!
            .ObterCobertura(CodigosCobertura.Casco)!.Taxa;
}

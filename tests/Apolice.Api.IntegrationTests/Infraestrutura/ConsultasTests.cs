using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Common;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Infrastructure.Outbox;
using Apolice.Infrastructure.Persistence;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

// O SQL do lado de leitura (Dapper) contra um "oráculo": os agregados carregados pelo EF com as regras do próprio
// domínio (StatusEfetivo) aplicadas em memória. Qualquer divergência de filtro, status efetivo, escopo ou keyset aparece.
public sealed class ConsultasTests(PostgresFixture postgres) : TesteComBanco(postgres, "consultas")
{
    private const int LimitePequeno = 3;

    protected override bool DadosDemo => true;

    private DateTimeOffset Agora => Ambiente.Tempo.GetUtcNow();

    private DateOnly Hoje => Relogio.HojeEm(Agora);

    public static TheoryData<string?, StatusEfetivoCotacao?, string?> FiltrosCotacoes() => new()
    {
        { null, null, null },
        { PersonasDemo.Corretora, null, null },
        { PersonasDemo.Corretor, null, null },
        { null, StatusEfetivoCotacao.Expirada, null },
        { null, StatusEfetivoCotacao.Calculada, null },
        { null, StatusEfetivoCotacao.Aceita, null },
        { null, StatusEfetivoCotacao.Emitida, CodigosProduto.Auto },
        { PersonasDemo.Corretora, StatusEfetivoCotacao.Recusada, null },
        { PersonasDemo.Corretor, StatusEfetivoCotacao.Expirada, CodigosProduto.Residencial },
    };

    [Theory]
    [MemberData(nameof(FiltrosCotacoes))]
    public async Task Deve_listar_cotacoes_como_o_dominio_quando_filtrar_e_paginar(string? persona, StatusEfetivoCotacao? status, string? produto)
    {
        var corretor = persona is null ? (Guid?)null : (await UsuarioAsync(persona)).Id;
        var cotacoes = await CarregarAsync(db => db.Cotacoes.AsNoTracking().Completas().ToListAsync(Ct));
        var esperado = cotacoes
            .Where(c => corretor is null || c.CorretorId == corretor)
            .Where(c => status is null || c.StatusEfetivo(Agora) == status)
            .Where(c => produto is null || c.ProdutoCodigo == produto)
            .OrderByDescending(c => c.CriadaEm).ThenByDescending(c => c.Id)
            .Select(c => c.Id)
            .ToList();

        var obtido = await TodasAsPaginasAsync<Application.Features.Cotacoes.CotacaoResumoDto>(apos =>
            NoEscopoAsync(sp => sp.GetRequiredService<ICotacaoQueries>().ListarAsync(new FiltroCotacoes(corretor, status, produto, null, apos, LimitePequeno, Agora), Ct)));

        obtido.Select(c => c.Id).ShouldBe(esperado);
        if (status is { } s)
        {
            obtido.ShouldAllBe(c => c.Status == s);
        }
    }

    public static TheoryData<string?, StatusEfetivoApolice?> FiltrosApolices() => new()
    {
        { null, null },
        { PersonasDemo.Corretora, null },
        { PersonasDemo.Corretor, null },
        { null, StatusEfetivoApolice.Ativa },
        { null, StatusEfetivoApolice.Programada },
        { null, StatusEfetivoApolice.Vencida },
        { PersonasDemo.Corretora, StatusEfetivoApolice.Cancelada },
        { PersonasDemo.Corretor, StatusEfetivoApolice.Cancelada },
    };

    [Theory]
    [MemberData(nameof(FiltrosApolices))]
    public async Task Deve_listar_apolices_com_status_efetivo_do_dominio_quando_filtrar_e_paginar(string? persona, StatusEfetivoApolice? status)
    {
        var corretor = persona is null ? (Guid?)null : (await UsuarioAsync(persona)).Id;
        var apolices = await CarregarAsync(db => db.Apolices.AsNoTracking().Completas().ToListAsync(Ct));
        var esperado = apolices
            .Where(a => corretor is null || a.CorretorId == corretor)
            .Where(a => status is null || a.StatusEfetivo(Hoje) == status)
            .OrderByDescending(a => a.EmitidaEm).ThenByDescending(a => a.Id)
            .ToList();

        var obtido = await TodasAsPaginasAsync<Application.Features.Apolices.ApoliceResumoDto>(apos =>
            NoEscopoAsync(sp => sp.GetRequiredService<IApoliceQueries>().ListarAsync(new FiltroApolices(corretor, status, null, null, apos, LimitePequeno, Hoje), Ct)));

        obtido.Select(a => a.Id).ShouldBe(esperado.Select(a => a.Id));
        obtido.Select(a => a.Status).ShouldBe(esperado.Select(a => a.StatusEfetivo(Hoje)));
        obtido.Select(a => a.SeguradoCpfMascarado).ShouldBe(esperado.Select(a => a.Segurado.Cpf.Mascarado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(StatusSinistro.Aberto)]
    [InlineData(StatusSinistro.EmAnalise)]
    [InlineData(StatusSinistro.Aprovado)]
    [InlineData(StatusSinistro.Negado)]
    [InlineData(StatusSinistro.Pago)]
    public async Task Deve_listar_sinistros_por_status_e_carteira_quando_filtrar(StatusSinistro? status)
    {
        var bruno = (await UsuarioAsync(PersonasDemo.Corretor)).Id;
        var sinistros = await CarregarAsync(db => db.Sinistros.AsNoTracking().ToListAsync(Ct));

        foreach (var corretor in new Guid?[] { null, bruno })
        {
            var esperado = sinistros
                .Where(s => corretor is null || s.CorretorId == corretor)
                .Where(s => status is null || s.Status == status)
                .OrderByDescending(s => s.AbertoEm).ThenByDescending(s => s.Id)
                .Select(s => s.Id);
            var obtido = await TodasAsPaginasAsync<Application.Features.Sinistros.SinistroResumoDto>(apos =>
                NoEscopoAsync(sp => sp.GetRequiredService<ISinistroQueries>().ListarAsync(new FiltroSinistros(corretor, status, null, apos, 2), Ct)));
            obtido.Select(s => s.Id).ShouldBe(esperado);
        }
    }

    [Theory]
    [InlineData("mariana")]
    [InlineData("SOUZA")]
    [InlineData("529.982")]
    [InlineData("52998224725")]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("Zé Ninguém")]
    public async Task Deve_buscar_cotacoes_por_nome_sem_caixa_e_por_cpf_com_ou_sem_mascara(string busca)
    {
        var cotacoes = await CarregarAsync(db => db.Cotacoes.AsNoTracking().ToListAsync(Ct));
        var digitos = new string([.. busca.Where(char.IsAsciiDigit)]);
        var soCpf = busca.All(c => char.IsAsciiDigit(c) || c is '.' or '-');
        var esperado = cotacoes
            .Where(c => c.Segurado.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase) || (soCpf && digitos.Length > 0 && c.Segurado.Cpf.Numero.Contains(digitos, StringComparison.Ordinal)))
            .Select(c => c.Id)
            .ToHashSet();

        var obtido = await NoEscopoAsync(sp => sp.GetRequiredService<ICotacaoQueries>().ListarAsync(new FiltroCotacoes(null, null, null, busca, null, 100, Agora), Ct));

        obtido.Itens.Select(c => c.Id).ToHashSet().SetEquals(esperado).ShouldBeTrue();
        if (busca is "mariana" or "529.982")
        {
            obtido.Itens.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public async Task Deve_buscar_apolices_e_sinistros_pelo_numero_quando_informado()
    {
        var apolice = (await CarregarAsync(db => db.Apolices.AsNoTracking().ToListAsync(Ct))).First();
        var sinistro = (await CarregarAsync(db => db.Sinistros.AsNoTracking().ToListAsync(Ct))).First();

        var apolices = await NoEscopoAsync(sp => sp.GetRequiredService<IApoliceQueries>().ListarAsync(new FiltroApolices(null, null, null, apolice.Numero.ToLowerInvariant(), null, 100, Hoje), Ct));
        var porApolice = await NoEscopoAsync(sp => sp.GetRequiredService<ISinistroQueries>().ListarAsync(new FiltroSinistros(null, null, sinistro.ApoliceNumero, null, 100), Ct));
        var porNumero = await NoEscopoAsync(sp => sp.GetRequiredService<ISinistroQueries>().ListarAsync(new FiltroSinistros(null, null, sinistro.Numero, null, 100), Ct));

        apolices.Itens.ShouldHaveSingleItem().Id.ShouldBe(apolice.Id);
        porApolice.Itens.ShouldContain(s => s.Id == sinistro.Id);
        porNumero.Itens.ShouldHaveSingleItem().Id.ShouldBe(sinistro.Id);
    }

    [Fact]
    public async Task Deve_esconder_detalhes_de_outra_carteira_e_mostrar_para_visao_global()
    {
        var ana = (await UsuarioAsync(PersonasDemo.Corretora)).Id;
        var bruno = (await UsuarioAsync(PersonasDemo.Corretor)).Id;
        var cotacao = (await CarregarAsync(db => db.Cotacoes.AsNoTracking().Where(c => c.CorretorId == ana).ToListAsync(Ct))).First();
        var apolice = (await CarregarAsync(db => db.Apolices.AsNoTracking().Where(a => a.CorretorId == ana).ToListAsync(Ct))).First();
        var sinistro = (await CarregarAsync(db => db.Sinistros.AsNoTracking().Where(s => s.CorretorId == ana).ToListAsync(Ct))).First();

        await NoEscopoAsync(async sp =>
        {
            var cotacoes = sp.GetRequiredService<ICotacaoQueries>();
            var apolices = sp.GetRequiredService<IApoliceQueries>();
            var sinistros = sp.GetRequiredService<ISinistroQueries>();
            (await cotacoes.ObterAsync(cotacao.Id, bruno, Agora, Ct)).ShouldBeNull();
            (await apolices.ObterAsync(apolice.Id, bruno, Hoje, Ct)).ShouldBeNull();
            (await sinistros.ObterAsync(sinistro.Id, bruno, Ct)).ShouldBeNull();
            (await cotacoes.ObterAsync(cotacao.Id, ana, Agora, Ct)).ShouldNotBeNull();
            (await apolices.ObterAsync(apolice.Id, null, Hoje, Ct)).ShouldNotBeNull();
            (await sinistros.ObterAsync(sinistro.Id, null, Ct)).ShouldNotBeNull();
            (await cotacoes.ObterAsync(Guid.CreateVersion7(), null, Agora, Ct)).ShouldBeNull();
        });
    }

    [Fact]
    public async Task Deve_montar_o_detalhe_da_apolice_com_endossos_sinistros_notificacoes_e_cronograma()
    {
        await Ambiente.ExecutarAsync<OutboxDispatcher, int>(d => d.DespacharPendentesAsync(500, Ct));
        var apolices = await CarregarAsync(db => db.Apolices.AsNoTracking().Completas().ToListAsync(Ct));
        var comEndosso = apolices.First(a => a.Endossos.Count > 0 && a.ProdutoCodigo == CodigosProduto.Residencial);

        var dto = (await NoEscopoAsync(sp => sp.GetRequiredService<IApoliceQueries>().ObterAsync(comEndosso.Id, null, Hoje, Ct)))!;

        dto.ProdutoNome.ShouldBe("Seguro Residencial");
        dto.CorretorNome.ShouldBe("Ana Ribeiro");
        dto.Segurado.Cpf.ShouldBe(comEndosso.Segurado.Cpf.Formatado);
        dto.Endossos.ShouldHaveSingleItem().CriadoPorNome.ShouldBe("Ana Ribeiro");
        dto.AjustesEndosso.ShouldBe(comEndosso.AjustesEndosso);
        dto.Coberturas.Select(c => c.Codigo).ShouldBe(comEndosso.Coberturas.Select(c => c.Codigo));
        dto.Coberturas[^1].Codigo.ShouldBe(CodigosCobertura.Vendaval);
        dto.CronogramaParcelas.Sum(p => p.Valor).ShouldBe(dto.PremioTotal);
        dto.Sinistros.ShouldNotBeEmpty();
        dto.Notificacoes.Select(n => n.Tipo).ShouldContain(Domain.Notificacoes.TipoNotificacao.BoasVindas);
        dto.Notificacoes.Select(n => n.Tipo).ShouldContain(Domain.Notificacoes.TipoNotificacao.SinistroDecidido);
        dto.Notificacoes.Select(n => n.CriadaEm).ShouldBeInOrder();
    }

    [Fact]
    public async Task Deve_trazer_historico_com_nomes_e_status_efetivo_no_detalhe_de_sinistro_e_cotacao()
    {
        var sinistros = await CarregarAsync(db => db.Sinistros.AsNoTracking().Completos().ToListAsync(Ct));
        var pago = sinistros.Single(s => s.Status == StatusSinistro.Pago);
        var expirada = (await CarregarAsync(db => db.Cotacoes.AsNoTracking().Completas().ToListAsync(Ct))).First(c => c.StatusEfetivo(Agora) == StatusEfetivoCotacao.Expirada);

        var sinistro = (await NoEscopoAsync(sp => sp.GetRequiredService<ISinistroQueries>().ObterAsync(pago.Id, null, Ct)))!;
        var cotacao = (await NoEscopoAsync(sp => sp.GetRequiredService<ICotacaoQueries>().ObterAsync(expirada.Id, null, Agora, Ct)))!;

        sinistro.Historico.Select(h => h.Status).ShouldBe([StatusSinistro.Aberto, StatusSinistro.EmAnalise, StatusSinistro.Aprovado, StatusSinistro.Pago]);
        sinistro.Historico[0].UsuarioNome.ShouldBe("Ana Ribeiro");
        sinistro.Historico[^1].UsuarioNome.ShouldBe("Carla Souza");
        sinistro.CorretorNome.ShouldBe("Ana Ribeiro");
        cotacao.Status.ShouldBe(StatusEfetivoCotacao.Expirada);
        cotacao.Itens.Select(i => i.Codigo).ShouldBe(expirada.Itens.Select(i => i.Codigo));
    }

    [Fact]
    public async Task Deve_calcular_o_dashboard_pelas_formulas_da_porta()
    {
        var ana = (await UsuarioAsync(PersonasDemo.Corretora)).Id;
        foreach (var corretor in new Guid?[] { null, ana })
        {
            var esperado = await DashboardEsperadoAsync(corretor);
            var obtido = await NoEscopoAsync(sp => sp.GetRequiredService<IDashboardQueries>().ObterAsync(new FiltroDashboard(corretor, Hoje, Agora), Ct));

            obtido.ApolicesAtivas.ShouldBe(esperado.ApolicesAtivas);
            obtido.PremioEmitido12Meses.ShouldBe(esperado.PremioEmitido12Meses);
            obtido.PremioEmitidoMes.ShouldBe(esperado.PremioEmitidoMes);
            obtido.CotacoesAbertas.ShouldBe(esperado.CotacoesAbertas);
            obtido.TaxaConversao90d.ShouldBe(esperado.TaxaConversao90d);
            obtido.SinistrosEmAberto.ShouldBe(esperado.SinistrosEmAberto);
            obtido.Sinistralidade12m.ShouldBe(esperado.Sinistralidade12m);
            obtido.PorProduto.ShouldBe(esperado.PorProduto);
            obtido.PremioEmitido12Meses.Count.ShouldBe(12);
            obtido.ApolicesAtivas.ShouldBeGreaterThan(0);
        }
    }

    private async Task<Application.Features.Dashboard.MetricasDashboard> DashboardEsperadoAsync(Guid? corretor)
    {
        var apolices = (await CarregarAsync(db => db.Apolices.AsNoTracking().ToListAsync(Ct))).Where(a => corretor is null || a.CorretorId == corretor).ToList();
        var cotacoes = (await CarregarAsync(db => db.Cotacoes.AsNoTracking().ToListAsync(Ct))).Where(c => corretor is null || c.CorretorId == corretor).ToList();
        var sinistros = (await CarregarAsync(db => db.Sinistros.AsNoTracking().ToListAsync(Ct))).Where(s => corretor is null || s.CorretorId == corretor).ToList();
        var produtos = await CarregarAsync(db => db.Produtos.AsNoTracking().OrderBy(p => p.Id).ToListAsync(Ct));

        var mesAtual = new DateOnly(Hoje.Year, Hoje.Month, 1);
        var meses = Enumerable.Range(0, 12).Select(i => mesAtual.AddMonths(i - 11)).ToList();
        bool NaJanela(DateTimeOffset instante) => Relogio.HojeEm(instante) >= meses[0] && Relogio.HojeEm(instante) < mesAtual.AddMonths(1);
        decimal NoMes(DateOnly mes) => apolices.Where(a => Relogio.HojeEm(a.EmitidaEm) is var d && d.Year == mes.Year && d.Month == mes.Month).Sum(a => a.PremioTotal);

        var mensal = meses.Select(m => new Application.Features.Dashboard.PremioMensalDto($"{m.Year:D4}-{m.Month:D2}", NoMes(m))).ToList();
        var premio = mensal.Sum(m => m.Valor);
        var criadas = cotacoes.Where(c => c.CriadaEm >= Agora.AddDays(-90)).ToList();
        var indenizacoes = sinistros.Where(s => s.Status is StatusSinistro.Aprovado or StatusSinistro.Pago && NaJanela(s.AbertoEm)).Sum(s => s.ValorIndenizacao ?? 0m);
        return new Application.Features.Dashboard.MetricasDashboard(
            apolices.Count(a => a.StatusEfetivo(Hoje) == StatusEfetivoApolice.Ativa),
            mensal[^1].Valor,
            mensal,
            cotacoes.Count(c => c.Status is StatusCotacao.Calculada or StatusCotacao.Aceita && c.ValidaAte >= Agora),
            criadas.Count == 0 ? 0m : Math.Round((decimal)criadas.Count(c => c.Status == StatusCotacao.Emitida) / criadas.Count, 4, MidpointRounding.AwayFromZero),
            sinistros.Count(s => s.Status is StatusSinistro.Aberto or StatusSinistro.EmAnalise),
            premio == 0 ? 0m : Math.Round(indenizacoes / premio, 4, MidpointRounding.AwayFromZero),
            [.. produtos.Select(p => new Application.Features.Dashboard.ProdutoDashboardDto(
                p.Codigo,
                p.Nome,
                apolices.Count(a => a.ProdutoCodigo == p.Codigo && a.StatusEfetivo(Hoje) == StatusEfetivoApolice.Ativa),
                apolices.Where(a => a.ProdutoCodigo == p.Codigo && NaJanela(a.EmitidaEm)).Sum(a => a.PremioTotal)))]);
    }

    private Task<T> CarregarAsync<T>(Func<ApoliceDbContext, Task<T>> consulta) => Ambiente.ExecutarAsync(consulta);

    private static async Task<List<T>> TodasAsPaginasAsync<T>(Func<PosicaoCursor?, Task<Pagina<T>>> buscar)
    {
        var itens = new List<T>();
        PosicaoCursor? apos = null;
        for (var paginas = 0; paginas < 100; paginas++)
        {
            var pagina = await buscar(apos);
            pagina.Itens.Count.ShouldBeLessThanOrEqualTo(LimitePequeno);
            itens.AddRange(pagina.Itens);
            if (pagina.ProximoCursor is null)
            {
                return itens;
            }

            apos = Cursor.Decodificar(pagina.ProximoCursor).Value;
        }

        throw new InvalidOperationException("Paginação não terminou.");
    }
}

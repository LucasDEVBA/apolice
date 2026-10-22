using Apolice.Application.Common;
using Apolice.Application.Features.Apolices.Listar;
using Apolice.Application.Features.Apolices.Obter;
using Apolice.Application.Features.Auditoria;
using Apolice.Application.Features.Auditoria.Listar;
using Apolice.Application.Features.Cotacoes.Listar;
using Apolice.Application.Features.Cotacoes.Obter;
using Apolice.Application.Features.Sinistros.Listar;
using Apolice.Application.Features.Sinistros.Obter;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Sinistros;
using NSubstitute;

namespace Apolice.Application.Tests.Consultas;

// O que importa aqui é o contrato handler → porta (escopo, filtros normalizados, cursor decodificado, limite) e o
// cursor produzido por Pagina.Montar. A fidelidade do SQL (keyset, filtros, busca) é dos testes de integração da infra.
public sealed class ConsultasPaginadasTests : TesteBase
{
    [Fact]
    public async Task Deve_repassar_a_carteira_e_o_cursor_decodificado_a_porta_quando_corretor_pagina_cotacoes()
    {
        for (var i = 0; i < 3; i++)
        {
            await Amb.CriarCotacaoAsync(Amb.Ana);
            Amb.Avancar(TimeSpan.FromMinutes(1));
        }

        await Amb.CriarCotacaoAsync(Amb.Bruno);
        Amb.Como(Amb.Ana);
        var handler = Amb.Servico<ListarCotacoesHandler>();

        var primeira = (await handler.HandleAsync(new ListarCotacoesQuery(Limite: 2), Ct)).DeveTerSucesso();
        var segunda = (await handler.HandleAsync(new ListarCotacoesQuery(Cursor: primeira.ProximoCursor, Limite: 2), Ct)).DeveTerSucesso();

        primeira.Itens.Count.ShouldBe(2);
        primeira.ProximoCursor.ShouldNotBeNull();
        segunda.Itens.ShouldHaveSingleItem();
        segunda.ProximoCursor.ShouldBeNull();
        primeira.Itens.Concat(segunda.Itens).ShouldAllBe(c => c.CorretorNome == "Ana Ribeiro");
        primeira.Itens[0].CriadaEm.ShouldBeGreaterThan(primeira.Itens[1].CriadaEm);
        var ultimaDaPrimeira = primeira.Itens[^1];
        Amb.Consultas.UltimoFiltroCotacoes.ShouldBe(new FiltroCotacoesEsperado(Amb.Ana.Id, null, null, null, new PosicaoCursor(ultimaDaPrimeira.CriadaEm, ultimaDaPrimeira.Id), 2, Amb.Agora).Filtro);
    }

    [Fact]
    public async Task Deve_repassar_filtros_normalizados_e_ver_tudo_quando_analista_lista_cotacoes()
    {
        await Amb.CriarCotacaoAsync(Amb.Ana);
        await Amb.CriarCotacaoAsync(Amb.Bruno);
        Amb.Avancar(TimeSpan.FromDays(31));
        Amb.Como(Amb.Carla);

        var pagina = (await Amb.Servico<ListarCotacoesHandler>()
            .HandleAsync(new ListarCotacoesQuery(StatusEfetivoCotacao.Expirada, " AUTO ", "  maria ", "", null), Ct)).DeveTerSucesso();

        pagina.Itens.Count.ShouldBe(2);
        pagina.Itens.ShouldAllBe(c => c.Status == StatusEfetivoCotacao.Expirada);
        Amb.Consultas.UltimoFiltroCotacoes.ShouldBe(new FiltroCotacoesEsperado(null, StatusEfetivoCotacao.Expirada, "AUTO", "maria", null, Paginacao.LimitePadrao, Amb.Agora).Filtro);
    }

    [Theory]
    [InlineData("cursor-invalido", null, "Comum.CursorInvalido")]
    [InlineData(null, 0, "Comum.LimiteInvalido")]
    [InlineData(null, 101, "Comum.LimiteInvalido")]
    public async Task Deve_retornar_erro_de_validacao_sem_consultar_quando_paginacao_invalida(string? cursor, int? limite, string codigo)
    {
        Amb.Como(Amb.Diego);

        var cotacoes = await Amb.Servico<ListarCotacoesHandler>().HandleAsync(new ListarCotacoesQuery(Cursor: cursor, Limite: limite), Ct);
        var apolices = await Amb.Servico<ListarApolicesHandler>().HandleAsync(new ListarApolicesQuery(Cursor: cursor, Limite: limite), Ct);
        var sinistros = await Amb.Servico<ListarSinistrosHandler>().HandleAsync(new ListarSinistrosQuery(Cursor: cursor, Limite: limite), Ct);
        var auditoria = await Amb.Servico<ListarAuditoriaHandler>().HandleAsync(new ListarAuditoriaQuery(cursor, limite), Ct);

        cotacoes.DeveFalharCom(codigo, ErrorType.Validacao);
        apolices.DeveFalharCom(codigo, ErrorType.Validacao);
        sinistros.DeveFalharCom(codigo, ErrorType.Validacao);
        auditoria.DeveFalharCom(codigo, ErrorType.Validacao);
        Amb.Consultas.UltimoFiltroCotacoes.ShouldBeNull();
        Amb.Consultas.UltimoFiltroApolices.ShouldBeNull();
        Amb.Consultas.UltimoFiltroSinistros.ShouldBeNull();
        await Amb.ConsultasAuditoria.DidNotReceiveWithAnyArgs().ListarAsync(default, default, Ct);
    }

    [Theory]
    [InlineData(Paginacao.LimiteMinimo)]
    [InlineData(Paginacao.LimiteMaximo)]
    public async Task Deve_aceitar_e_repassar_a_porta_quando_limite_esta_nas_bordas(int limite)
    {
        Amb.Como(Amb.Diego);
        Amb.ConsultasAuditoria.ListarAsync(Arg.Any<PosicaoCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Pagina.Vazia<AuditoriaDto>());

        (await Amb.Servico<ListarCotacoesHandler>().HandleAsync(new ListarCotacoesQuery(Limite: limite), Ct)).DeveTerSucesso();
        (await Amb.Servico<ListarApolicesHandler>().HandleAsync(new ListarApolicesQuery(Limite: limite), Ct)).DeveTerSucesso();
        (await Amb.Servico<ListarSinistrosHandler>().HandleAsync(new ListarSinistrosQuery(Limite: limite), Ct)).DeveTerSucesso();
        (await Amb.Servico<ListarAuditoriaHandler>().HandleAsync(new ListarAuditoriaQuery(Limite: limite), Ct)).DeveTerSucesso();

        Amb.Consultas.UltimoFiltroCotacoes!.Limite.ShouldBe(limite);
        Amb.Consultas.UltimoFiltroApolices!.Limite.ShouldBe(limite);
        Amb.Consultas.UltimoFiltroSinistros!.Limite.ShouldBe(limite);
        await Amb.ConsultasAuditoria.Received(1).ListarAsync(null, limite, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_quando_anonimo_consulta()
    {
        Amb.UsuarioAtual.Anonimo();

        (await Amb.Servico<ListarCotacoesHandler>().HandleAsync(new ListarCotacoesQuery(), Ct)).DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
        (await Amb.Servico<ListarApolicesHandler>().HandleAsync(new ListarApolicesQuery(), Ct)).DeveFalharCom("Auth.NaoAutenticado");
        (await Amb.Servico<ListarSinistrosHandler>().HandleAsync(new ListarSinistrosQuery(), Ct)).DeveFalharCom("Auth.NaoAutenticado");
        (await Amb.Servico<ObterCotacaoHandler>().HandleAsync(new ObterCotacaoQuery(Guid.NewGuid()), Ct)).DeveFalharCom("Auth.NaoAutenticado");
        (await Amb.Servico<ObterApoliceHandler>().HandleAsync(new ObterApoliceQuery(Guid.NewGuid()), Ct)).DeveFalharCom("Auth.NaoAutenticado");
        (await Amb.Servico<ObterSinistroHandler>().HandleAsync(new ObterSinistroQuery(Guid.NewGuid()), Ct)).DeveFalharCom("Auth.NaoAutenticado");
    }

    [Fact]
    public async Task Deve_listar_apolices_com_cpf_mascarado_e_status_efetivo_quando_corretor_consulta()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.EmitirApoliceAsync(Amb.Bruno);
        Amb.Como(Amb.Ana);

        var pagina = (await Amb.Servico<ListarApolicesHandler>()
            .HandleAsync(new ListarApolicesQuery(StatusEfetivoApolice.Ativa, "AUTO", null, null, 10), Ct)).DeveTerSucesso();

        var resumo = pagina.Itens.ShouldHaveSingleItem();
        resumo.Id.ShouldBe(apolice.Id);
        resumo.SeguradoCpfMascarado.ShouldBe("***.982.247-**");
        resumo.Status.ShouldBe(StatusEfetivoApolice.Ativa);
        Amb.Consultas.UltimoFiltroApolices!.CorretorId.ShouldBe(Amb.Ana.Id);
        Amb.Consultas.UltimoFiltroApolices.Hoje.ShouldBe(Amb.Hoje);
    }

    [Fact]
    public async Task Deve_listar_sinistros_do_escopo_com_score_de_triagem_quando_analista_consulta()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        await Amb.ProcessarOutboxAsync();
        Amb.Como(Amb.Carla);

        var pagina = (await Amb.Servico<ListarSinistrosHandler>()
            .HandleAsync(new ListarSinistrosQuery(StatusSinistro.Aberto, " SIN ", null, 5), Ct)).DeveTerSucesso();

        pagina.Itens.ShouldHaveSingleItem().TriagemScore.ShouldBe(40);
        Amb.Consultas.UltimoFiltroSinistros.ShouldBe(new FiltroSinistrosEsperado(null, StatusSinistro.Aberto, "SIN", null, 5).Filtro);
    }

    // Registros auxiliares só para montar o filtro esperado com nomes de parâmetro legíveis no teste.
    private sealed record FiltroCotacoesEsperado(Guid? CorretorId, StatusEfetivoCotacao? Status, string? Produto, string? Busca, PosicaoCursor? Apos, int Limite, DateTimeOffset Agora)
    {
        public Abstractions.Queries.FiltroCotacoes Filtro => new(CorretorId, Status, Produto, Busca, Apos, Limite, Agora);
    }

    private sealed record FiltroSinistrosEsperado(Guid? CorretorId, StatusSinistro? Status, string? Busca, PosicaoCursor? Apos, int Limite)
    {
        public Abstractions.Queries.FiltroSinistros Filtro => new(CorretorId, Status, Busca, Apos, Limite);
    }
}

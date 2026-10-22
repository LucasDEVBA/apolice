using System.Text.Json;
using System.Text.Json.Nodes;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Apolices.Listar;
using Apolice.Application.Features.Apolices.SimularCancelamento;
using Apolice.Application.Features.Apolices.SimularEndosso;
using Apolice.Application.Features.Auditoria;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Listar;
using Apolice.Application.Features.Dashboard;
using Apolice.Application.Features.Produtos.Obter;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Common;

// Os nomes de campo são contrato com o frontend (SPEC 7.1): renomear uma propriedade quebra o cliente gerado.
public sealed class ContratoJsonTests : TesteBase
{
    [Fact]
    public async Task Deve_expor_os_campos_do_spec_quando_detalhe_da_apolice_vira_json()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana, parcelas: 2);
        var sinistro = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        Amb.Avancar(TimeSpan.FromDays(73));
        await Amb.Servico<EndossarApoliceHandler>().HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 200_000m), Ct);
        await Amb.ProcessarOutboxAsync();
        var detalhe = (await Amb.Servico<Features.Apolices.Obter.ObterApoliceHandler>().HandleAsync(new(apolice.Id), Ct)).DeveTerSucesso();

        var json = Serializar(detalhe);

        Campos(json).ShouldBe(
        [
            "id", "numero", "cotacaoId", "produtoCodigo", "produtoNome", "segurado", "dadosRisco", "vigenciaInicio", "vigenciaFim",
            "coberturas", "premioLiquido", "ajustesEndosso", "premioLiquidoVigente", "aliquotaIof", "iof", "premioTotal", "parcelas",
            "cronogramaParcelas", "status", "emitidaEm", "canceladaEm", "motivoCancelamento", "valorRestituicao", "boasVindasEnviadasEm",
            "corretorId", "corretorNome", "endossos", "sinistros", "notificacoes",
        ]);
        json["dadosRisco"]!["produto"]!.GetValue<string>().ShouldBe("AUTO");
        json["status"]!.GetValue<string>().ShouldBe("Ativa");
        json["vigenciaInicio"]!.GetValue<string>().ShouldBe("2026-03-10");
        Campos(json["cronogramaParcelas"]![0]!).ShouldBe(["numero", "vencimento", "valor"]);
        Campos(json["sinistros"]![0]!).ShouldBe(["id", "numero", "apoliceNumero", "seguradoNome", "coberturaNome", "valorReclamado", "status", "triagemScore", "abertoEm"]);
        Campos(json["notificacoes"]![0]!).ShouldBe(["id", "tipo", "destinatario", "assunto", "corpo", "criadaEm"]);
        Campos(json["segurado"]!).ShouldBe(["nome", "cpf", "email", "dataNascimento"]);
        Campos(json["endossos"]![0]!).ShouldBe(
        [
            "id", "numero", "tipo", "coberturaCodigo", "coberturaNome", "lmiAnterior", "lmiNovo", "dataEfeito", "ajusteLiquido", "iofAjuste",
            "ajusteTotal", "criadoEm", "criadoPorNome",
        ]);
        Campos(Serializar(sinistro)).ShouldBe(
        [
            "id", "numero", "apoliceId", "apoliceNumero", "produtoCodigo", "seguradoNome", "coberturaCodigo", "coberturaNome", "lmiCobertura",
            "dataOcorrencia", "descricao", "valorReclamado", "valorIndenizacao", "status", "motivoNegativa", "triagem", "abertoEm", "corretorNome", "historico",
        ]);
    }

    // RCF_DM de 100.000 para 200.000 no dia 73 (292 dias restantes): (1.200 − 600) × 292 ÷ 365 = 480,00; IOF 35,42.
    [Fact]
    public async Task Deve_mapear_cada_campo_do_endosso_no_seu_lugar_quando_detalhe_traz_endossos()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(73));
        var criadoEm = Amb.Agora;

        var endossada = (await Amb.Servico<EndossarApoliceHandler>()
            .HandleAsync(new EndossarApoliceCommand(apolice.Id, TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 200_000m), Ct)).DeveTerSucesso();

        var endosso = endossada.Endossos.ShouldHaveSingleItem();
        endosso.Tipo.ShouldBe(TipoEndosso.AlteracaoLmi);
        endosso.CoberturaCodigo.ShouldBe(CodigosCobertura.RcfDanosMateriais);
        endosso.LmiAnterior.ShouldBe(100_000m);
        endosso.LmiNovo.ShouldBe(200_000m);
        endosso.DataEfeito.ShouldBe(new DateOnly(2026, 5, 22));
        endosso.AjusteLiquido.ShouldBe(480.00m);
        endosso.IofAjuste.ShouldBe(35.42m);
        endosso.AjusteTotal.ShouldBe(515.42m);
        endosso.CriadoEm.ShouldBe(criadoEm);
    }

    [Fact]
    public async Task Deve_expor_os_campos_e_valores_do_spec_quando_pagina_de_apolices_vira_json()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var pagina = (await Amb.Servico<ListarApolicesHandler>().HandleAsync(new ListarApolicesQuery(), Ct)).DeveTerSucesso();

        var json = Serializar(pagina);
        Campos(json).ShouldBe(["itens", "proximoCursor"]);
        var item = json["itens"]![0]!;
        Campos(item).ShouldBe(["id", "numero", "produtoCodigo", "seguradoNome", "seguradoCpfMascarado", "premioTotal", "vigenciaInicio", "vigenciaFim", "status", "corretorNome", "emitidaEm"]);
        item["vigenciaInicio"]!.GetValue<string>().ShouldBe("2026-03-10");
        item["vigenciaFim"]!.GetValue<string>().ShouldBe("2027-03-10");
        item["seguradoCpfMascarado"]!.GetValue<string>().ShouldBe("***.982.247-**");
        item["numero"]!.GetValue<string>().ShouldBe(apolice.Numero);
    }

    [Fact]
    public async Task Deve_expor_os_campos_do_spec_quando_cotacao_resumo_e_produto_viram_json()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);
        var aceita = (await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new(cotacao.Id), Ct)).DeveTerSucesso();
        var pagina = (await Amb.Servico<ListarCotacoesHandler>().HandleAsync(new ListarCotacoesQuery(), Ct)).DeveTerSucesso();
        var produto = (await Amb.Servico<ObterProdutoHandler>().HandleAsync(new ObterProdutoQuery(CodigosProduto.Auto), Ct)).DeveTerSucesso();

        Campos(Serializar(aceita)).ShouldBe(
        [
            "id", "produtoCodigo", "produtoNome", "segurado", "dadosRisco", "itens", "fatores", "premioLiquido", "aliquotaIof", "iof",
            "premioTotal", "maxParcelas", "status", "validaAte", "criadaEm", "corretorId", "corretorNome", "apoliceId",
        ]);
        var paginaJson = Serializar(pagina);
        Campos(paginaJson).ShouldBe(["itens", "proximoCursor"]);
        Campos(paginaJson["itens"]![0]!).ShouldBe(["id", "produtoCodigo", "seguradoNome", "premioTotal", "status", "validaAte", "criadaEm", "corretorNome"]);
        Campos(Serializar(produto)["coberturas"]![0]!).ShouldBe(
            ["codigo", "nome", "descricao", "obrigatoria", "tipoCalculo", "taxa", "premioFixo", "lmiMinimo", "lmiMaximo", "lmiFixo", "permiteSinistro"]);
    }

    [Fact]
    public void Deve_expor_os_campos_do_spec_quando_dtos_sem_fluxo_viram_json()
    {
        var agora = DateTimeOffset.UnixEpoch;
        var dashboard = new DashboardDto(EscopoDashboard.Global, 1, 2m, [new("2026-10", 3m)], 4, 0.5m, 5, 0.25m, [new("AUTO", "Seguro Auto", 1, 2m)]);
        var simulacaoEndosso = new SimulacaoEndossoDto(new DateOnly(2026, 5, 22), 292, 365, 0m, 220m, 176m, 12.99m, 188.99m);
        var simulacaoCancelamento = new SimulacaoCancelamentoDto(new DateOnly(2026, 3, 30), 20, 365, MetodoCancelamento.PrazoCurto, 0.2m, 3_800m, 3_040m);
        var auditoria = new AuditoriaDto(Guid.Empty, null, "auth.login", "Usuario", null, null, null, agora);
        var auth = new AuthResponse("token", agora, new UsuarioDto(Guid.Empty, "Ana", "ana@x.com", Domain.Usuarios.Papel.Corretor));

        Campos(Serializar(dashboard)).ShouldBe(
            ["escopo", "apolicesAtivas", "premioEmitidoMes", "premioEmitido12Meses", "cotacoesAbertas", "taxaConversao90d", "sinistrosEmAberto", "sinistralidade12m", "porProduto"]);
        Serializar(dashboard)["escopo"]!.GetValue<string>().ShouldBe("Global");
        Campos(Serializar(dashboard)["porProduto"]![0]!).ShouldBe(["produtoCodigo", "produtoNome", "apolicesAtivas", "premioEmitido"]);
        Campos(Serializar(simulacaoEndosso)).ShouldBe(["dataEfeito", "diasRestantes", "diasVigencia", "premioAnualAnterior", "premioAnualNovo", "ajusteLiquido", "iofAjuste", "ajusteTotal"]);
        Campos(Serializar(simulacaoCancelamento)).ShouldBe(["dataCancelamento", "diasDecorridos", "diasVigencia", "metodo", "percentualRetido", "premioLiquidoBase", "valorRestituicao"]);
        Campos(Serializar(auditoria)).ShouldBe(["id", "usuarioNome", "acao", "entidade", "entidadeId", "detalhes", "correlationId", "ocorridoEm"]);
        Campos(Serializar(auth)).ShouldBe(["accessToken", "expiraEm", "usuario"]);
        Serializar(auth)["usuario"]!["papel"]!.GetValue<string>().ShouldBe("Corretor");
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_quando_anonimo_tenta_operar_recurso_existente()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);
        Amb.UsuarioAtual.Anonimo();

        var resultado = await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);

        resultado.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }

    // A infra converte DbUpdateConcurrencyException nesta exceção (com a original como InnerException) e a API a
    // traduz para o erro Concorrencia.Conflito → 409.
    [Fact]
    public void Deve_carregar_a_causa_e_mapear_para_409_quando_conflito_de_concorrencia()
    {
        var interna = new InvalidOperationException("versão");

        new ConflitoDeConcorrenciaException().Message.ShouldNotBeNullOrWhiteSpace();
        new ConflitoDeConcorrenciaException("msg").Message.ShouldBe("msg");
        new ConflitoDeConcorrenciaException("msg", interna).InnerException.ShouldBeSameAs(interna);
        ConcorrenciaErros.Conflito.ShouldBe(new Error("Concorrencia.Conflito", ConcorrenciaErros.Conflito.Mensagem, ErrorType.Conflito));
    }

    // Fonte única dos usuários de demonstração do SPEC 6: o login demo e o DataSeeder da infra leem daqui.
    [Fact]
    public void Deve_descrever_os_usuarios_de_demonstracao_do_spec_quando_seed_consulta_as_personas()
    {
        PersonasDemo.Usuarios.Select(u => (u.Persona, u.Email, u.Nome, u.Papel)).ShouldBe(
        [
            ("corretora", "ana.corretora@apolice.dev", "Ana Ribeiro", Papel.Corretor),
            ("corretor", "bruno.corretor@apolice.dev", "Bruno Lima", Papel.Corretor),
            ("analista", "carla.analista@apolice.dev", "Carla Souza", Papel.Analista),
            ("admin", "admin@apolice.dev", "Diego Martins", Papel.Admin),
        ]);
        PersonasDemo.Todas.Order(StringComparer.Ordinal).ShouldBe(["admin", "analista", "corretor", "corretora"]);
    }

    private static JsonNode Serializar<T>(T valor) => JsonNode.Parse(JsonSerializer.Serialize(valor, JsonOpcoes.Padrao))!;

    private static string[] Campos(JsonNode no) => [.. no.AsObject().Select(p => p.Key)];
}

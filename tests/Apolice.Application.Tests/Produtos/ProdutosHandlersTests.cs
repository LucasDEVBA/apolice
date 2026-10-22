using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Application.Features.Produtos.AtualizarTaxa;
using Apolice.Application.Features.Produtos.Listar;
using Apolice.Application.Features.Produtos.Obter;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Application.Tests.Produtos;

public sealed class ProdutosHandlersTests : TesteBase
{
    [Fact]
    public async Task Deve_listar_os_tres_produtos_com_coberturas_quando_consulta_catalogo()
    {
        var produtos = (await Amb.Servico<ListarProdutosHandler>().HandleAsync(new ListarProdutosQuery(), Ct)).DeveTerSucesso();

        produtos.Select(p => p.Codigo).ShouldBe([CodigosProduto.Auto, CodigosProduto.Residencial, CodigosProduto.Vida]);
        var vidros = produtos[0].Coberturas.Single(c => c.Codigo == CodigosCobertura.Vidros);
        vidros.TipoCalculo.ShouldBe(TipoCalculo.PremioFixo);
        vidros.PremioFixo.ShouldBe(220.00m);
        vidros.LmiFixo.ShouldBe(3_000m);
        produtos[0].AliquotaIof.ShouldBe(0.0738m);
    }

    [Theory]
    [InlineData("vida")]
    [InlineData(" VIDA ")]
    public async Task Deve_obter_o_produto_quando_codigo_em_qualquer_caixa(string codigo)
    {
        var produto = (await Amb.Servico<ObterProdutoHandler>().HandleAsync(new ObterProdutoQuery(codigo), Ct)).DeveTerSucesso();

        produto.Nome.ShouldBe("Seguro de Vida");
        produto.Coberturas.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("MOTO")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Deve_retornar_nao_encontrado_quando_produto_inexistente(string? codigo)
    {
        var resultado = await Amb.Servico<ObterProdutoHandler>().HandleAsync(new ObterProdutoQuery(codigo!), Ct);

        resultado.DeveFalharCom(ComumErros.NaoEncontrado(Entidades.Produto).Codigo, ErrorType.NaoEncontrado);
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_quando_anonimo_consulta_o_catalogo()
    {
        Amb.UsuarioAtual.Anonimo();

        var lista = await Amb.Servico<ListarProdutosHandler>().HandleAsync(new ListarProdutosQuery(), Ct);
        var produto = await Amb.Servico<ObterProdutoHandler>().HandleAsync(new ObterProdutoQuery(CodigosProduto.Auto), Ct);

        lista.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
        produto.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }

    [Fact]
    public async Task Deve_atualizar_auditar_e_invalidar_cache_depois_do_commit_quando_admin_altera_taxa()
    {
        Amb.Como(Amb.Diego);
        Amb.Catalogo.Aquecer();
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Amb.Servico<AtualizarTaxaCoberturaHandler>()
            .HandleAsync(new AtualizarTaxaCoberturaCommand("auto", "casco", 0.0350m), Ct);

        var produto = resultado.DeveTerSucesso();
        produto.Coberturas.Single(c => c.Codigo == CodigosCobertura.Casco).Taxa.ShouldBe(0.0350m);
        Amb.Banco.Produtos.Single(p => p.Codigo == CodigosProduto.Auto).ObterCobertura(CodigosCobertura.Casco)!.Taxa.ShouldBe(0.0350m);
        var auditoria = Amb.Auditoria.Registros.ShouldHaveSingleItem();
        auditoria.Acao.ShouldBe(AcoesAuditoria.TaxaAtualizada);
        auditoria.EntidadeId.ShouldBe(CodigosProduto.Auto);
        auditoria.Detalhes.DeveSerJsonEquivalente("""{"cobertura":"CASCO","taxaAnterior":0.032,"taxaNova":0.035}""");
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
        Amb.Catalogo.SavesNaInvalidacao.ShouldBe([savesAntes + 1]);
        (await Amb.Catalogo.ObterAsync(CodigosProduto.Auto, Ct))!.ObterCobertura(CodigosCobertura.Casco)!.Taxa.ShouldBe(0.0350m);
        Amb.Banco.Outbox.ShouldHaveSingleItem().ShouldBeOfType<TaxaCoberturaAtualizada>();
    }

    [Fact]
    public async Task Deve_nao_gravar_nem_invalidar_quando_taxa_igual_a_atual()
    {
        Amb.Como(Amb.Diego);
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Amb.Servico<AtualizarTaxaCoberturaHandler>()
            .HandleAsync(new AtualizarTaxaCoberturaCommand(CodigosProduto.Auto, CodigosCobertura.Casco, 0.0320m), Ct);

        resultado.DeveTerSucesso();
        Amb.Auditoria.Registros.ShouldBeEmpty();
        Amb.Catalogo.Invalidacoes.ShouldBe(0);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Banco.Outbox.ShouldBeEmpty();
    }

    // A taxa já está gravada: falhar a resposta faria o cliente repetir um pedido que vira no-op e nunca invalidaria.
    [Fact]
    public async Task Deve_responder_sucesso_e_avisar_no_log_quando_invalidacao_do_cache_falha()
    {
        Amb.Como(Amb.Diego);
        Amb.Catalogo.FalharInvalidacao = true;

        var resultado = await Amb.Servico<AtualizarTaxaCoberturaHandler>()
            .HandleAsync(new AtualizarTaxaCoberturaCommand(CodigosProduto.Auto, CodigosCobertura.Casco, 0.0350m), Ct);

        resultado.DeveTerSucesso().Coberturas.Single(c => c.Codigo == CodigosCobertura.Casco).Taxa.ShouldBe(0.0350m);
        Amb.Banco.Produtos.Single(p => p.Codigo == CodigosProduto.Auto).ObterCobertura(CodigosCobertura.Casco)!.Taxa.ShouldBe(0.0350m);
        Amb.Logs.Mensagens.ShouldContain(m => m.StartsWith("Warning", StringComparison.Ordinal) && m.Contains("invalidação do catálogo falhou", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("AUTO", "VIDROS", 0.01, "Produto.CoberturaDePremioFixo", ErrorType.RegraDeNegocio)]
    [InlineData("AUTO", "INEXISTENTE", 0.01, "Produto.CoberturaNaoEncontrada", ErrorType.NaoEncontrado)]
    [InlineData("AUTO", "", 0.01, "Produto.CoberturaNaoEncontrada", ErrorType.NaoEncontrado)]
    [InlineData("AUTO", "CASCO", 0, "Produto.TaxaInvalida", ErrorType.Validacao)]
    [InlineData("MOTO", "CASCO", 0.01, "Comum.NaoEncontrado", ErrorType.NaoEncontrado)]
    [InlineData("", "CASCO", 0.01, "Comum.NaoEncontrado", ErrorType.NaoEncontrado)]
    public async Task Deve_propagar_erro_do_dominio_quando_alteracao_invalida(string produto, string cobertura, double taxa, string codigo, ErrorType tipo)
    {
        Amb.Como(Amb.Diego);
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Amb.Servico<AtualizarTaxaCoberturaHandler>()
            .HandleAsync(new AtualizarTaxaCoberturaCommand(produto, cobertura, (decimal)taxa), Ct);

        resultado.DeveFalharCom(codigo, tipo);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Catalogo.Invalidacoes.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_proibir_quando_quem_altera_taxa_nao_e_admin()
    {
        Amb.Como(Amb.Carla);
        var analista = await Amb.Servico<AtualizarTaxaCoberturaHandler>().HandleAsync(new AtualizarTaxaCoberturaCommand("AUTO", "CASCO", 0.04m), Ct);
        Amb.UsuarioAtual.Anonimo();
        var anonimo = await Amb.Servico<AtualizarTaxaCoberturaHandler>().HandleAsync(new AtualizarTaxaCoberturaCommand("AUTO", "CASCO", 0.04m), Ct);

        analista.DeveFalharCom(ComumErros.Proibido.Codigo, ErrorType.Proibido);
        anonimo.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }
}

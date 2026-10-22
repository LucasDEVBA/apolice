using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices.GerarDocumento;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;

namespace Apolice.Application.Tests.Apolices;

public sealed class GerarDocumentoApoliceHandlerTests : TesteBase
{
    [Fact]
    public async Task Deve_gerar_pdf_a_partir_do_mesmo_dto_do_detalhe_quando_flag_ligada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var documento = (await Amb.Servico<GerarDocumentoApoliceHandler>().HandleAsync(new GerarDocumentoApoliceQuery(apolice.Id), Ct)).DeveTerSucesso();

        documento.NomeArquivo.ShouldBe("apolice-AUTO-2026-000001.pdf");
        documento.TipoConteudo.ShouldBe("application/pdf");
        documento.Conteudo.ShouldNotBeEmpty();
        var usado = Amb.Gerador.UltimaApolice.ShouldNotBeNull();
        usado.Id.ShouldBe(apolice.Id);
        usado.Segurado.Cpf.ShouldBe(Dados.CpfValido);
        usado.CronogramaParcelas.ShouldBe(apolice.CronogramaParcelas);
    }

    [Fact]
    public async Task Deve_retornar_desabilitada_quando_flag_documento_pdf_desligada()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Flags.Desligar(FeatureFlags.DocumentoPdf);

        var resultado = await Amb.Servico<GerarDocumentoApoliceHandler>().HandleAsync(new GerarDocumentoApoliceQuery(apolice.Id), Ct);

        resultado.DeveFalharCom("Comum.FuncionalidadeDesabilitada", ErrorType.NaoEncontrado);
        Amb.Gerador.UltimaApolice.ShouldBeNull();
    }

    [Fact]
    public async Task Deve_exigir_autenticacao_quando_anonimo_pede_documento()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.UsuarioAtual.Anonimo();

        var resultado = await Amb.Servico<GerarDocumentoApoliceHandler>().HandleAsync(new GerarDocumentoApoliceQuery(apolice.Id), Ct);

        resultado.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }
}

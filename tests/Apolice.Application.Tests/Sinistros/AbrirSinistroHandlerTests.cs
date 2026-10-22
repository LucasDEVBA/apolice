using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Sinistros;

public sealed class AbrirSinistroHandlerTests : TesteBase
{
    [Fact]
    public async Task Deve_abrir_numerar_auditar_e_devolver_detalhe_quando_corretor_dono_avisa()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(5));

        var sinistro = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id, 8_000m, Amb.Hoje.AddDays(-1));

        sinistro.Numero.ShouldBe("SIN-2026-000001");
        sinistro.ApoliceNumero.ShouldBe(apolice.Numero);
        sinistro.Status.ShouldBe(StatusSinistro.Aberto);
        sinistro.CoberturaNome.ShouldBe("Colisão, incêndio e roubo");
        sinistro.LmiCobertura.ShouldBe(100_000m);
        sinistro.CorretorNome.ShouldBe("Ana Ribeiro");
        sinistro.Triagem.ShouldBeNull();
        var historico = sinistro.Historico.ShouldHaveSingleItem();
        historico.UsuarioNome.ShouldBe("Ana Ribeiro");
        Amb.Banco.Numeradores["SINISTRO:2026"].ShouldBe(1);
        Amb.Auditoria.Registros.Last().Acao.ShouldBe(AcoesAuditoria.SinistroAberto);
        Amb.Banco.Outbox.OfType<SinistroAberto>().ShouldHaveSingleItem().SinistroId.ShouldBe(sinistro.Id);
    }

    [Theory]
    [InlineData(CodigosCobertura.Vidros, 0, Ambiente.DescricaoSinistro, 100.0, "Sinistro.CoberturaNaoContratada", ErrorType.RegraDeNegocio)]
    [InlineData(CodigosCobertura.Casco, 1, Ambiente.DescricaoSinistro, 100.0, "Sinistro.OcorrenciaFutura", ErrorType.Validacao)]
    [InlineData(CodigosCobertura.Casco, -1, Ambiente.DescricaoSinistro, 100.0, "Sinistro.ForaDaVigencia", ErrorType.RegraDeNegocio)]
    [InlineData(CodigosCobertura.Casco, 0, "curta", 100.0, "Sinistro.DescricaoInvalida", ErrorType.Validacao)]
    [InlineData(CodigosCobertura.Casco, 0, Ambiente.DescricaoSinistro, 0.0, "Sinistro.ValorReclamadoInvalido", ErrorType.Validacao)]
    [InlineData(null, 0, Ambiente.DescricaoSinistro, 100.0, "Sinistro.CoberturaNaoContratada", ErrorType.RegraDeNegocio)]
    public async Task Deve_propagar_regra_do_dominio_quando_aviso_invalido(string? cobertura, int diasOcorrencia, string descricao, double valor, string codigo, ErrorType tipo)
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var resultado = await Amb.Servico<AbrirSinistroHandler>().HandleAsync(
            new AbrirSinistroCommand(apolice.Id, cobertura!, Amb.Hoje.AddDays(diasOcorrencia), descricao, (decimal)valor), Ct);

        resultado.DeveFalharCom(codigo, tipo);
        Amb.Banco.Sinistros.ShouldBeEmpty();
    }

    // Só o domínio conhece as regras do aviso e a fábrica já exige o número: aviso recusado deixa lacuna (SPEC 6).
    [Fact]
    public async Task Deve_deixar_lacuna_na_numeracao_quando_dominio_recusa_o_aviso()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        var recusado = await Amb.Servico<AbrirSinistroHandler>().HandleAsync(
            new AbrirSinistroCommand(apolice.Id, CodigosCobertura.Casco, Amb.Hoje.AddDays(1), Ambiente.DescricaoSinistro, 100m), Ct);
        var aceito = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);

        recusado.DeveFalharCom("Sinistro.OcorrenciaFutura", ErrorType.Validacao);
        aceito.Numero.ShouldBe("SIN-2026-000002");
        Amb.Banco.Sinistros.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Deve_recusar_cobertura_de_servico_quando_assistencia_nao_admite_sinistro()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana, request: Dados.CotacaoAuto(new CoberturaRequest(CodigosCobertura.Assistencia24h, null)));

        var resultado = await Amb.Servico<AbrirSinistroHandler>().HandleAsync(
            new AbrirSinistroCommand(apolice.Id, "assistencia_24h", Amb.Hoje, Ambiente.DescricaoSinistro, 300m), Ct);

        resultado.DeveFalharCom("Sinistro.CoberturaSemIndenizacao", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public async Task Deve_proibir_analista_e_negar_existencia_de_apolice_inexistente_quando_avisam()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        Amb.Como(Amb.Carla);
        var analista = await Amb.Servico<AbrirSinistroHandler>().HandleAsync(new AbrirSinistroCommand(apolice.Id, CodigosCobertura.Casco, Amb.Hoje, Ambiente.DescricaoSinistro, 100m), Ct);
        Amb.Como(Amb.Ana);
        var inexistente = await Amb.Servico<AbrirSinistroHandler>().HandleAsync(new AbrirSinistroCommand(Guid.NewGuid(), CodigosCobertura.Casco, Amb.Hoje, Ambiente.DescricaoSinistro, 100m), Ct);

        analista.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        inexistente.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
        Amb.Banco.Numeradores.ShouldNotContainKey("SINISTRO:2026");
    }
}

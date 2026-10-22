using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Sinistros;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.IniciarAnalise;
using Apolice.Application.Features.Sinistros.Negar;
using Apolice.Application.Features.Sinistros.RegistrarPagamento;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Sinistros;

public sealed class AcoesSinistroTests : TesteBase
{
    private const string MotivoNegativa = "Dano preexistente à contratação, constatado na vistoria.";

    [Fact]
    public async Task Deve_percorrer_analise_aprovacao_e_pagamento_quando_analista_decide()
    {
        var sinistro = await SinistroAbertoAsync();
        Amb.Como(Amb.Carla);

        var emAnalise = (await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(sinistro.Id), Ct)).DeveTerSucesso();
        var aprovado = (await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(sinistro.Id, 7_500m, "  Orçamento conferido.  "), Ct)).DeveTerSucesso();
        var pago = (await Amb.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(sinistro.Id), Ct)).DeveTerSucesso();

        emAnalise.Status.ShouldBe(StatusSinistro.EmAnalise);
        aprovado.ValorIndenizacao.ShouldBe(7_500m);
        pago.Status.ShouldBe(StatusSinistro.Pago);
        pago.Historico.Select(h => (h.Status, h.UsuarioNome)).ShouldBe(
        [
            (StatusSinistro.Aberto, "Ana Ribeiro"),
            (StatusSinistro.EmAnalise, "Carla Souza"),
            (StatusSinistro.Aprovado, "Carla Souza"),
            (StatusSinistro.Pago, "Carla Souza"),
        ]);
        pago.Historico[2].Observacao.ShouldBe("Orçamento conferido.");
        var ultimas = Amb.Auditoria.Registros.TakeLast(3).ToList();
        ultimas.Select(r => r.Acao).ShouldBe([AcoesAuditoria.SinistroEmAnalise, AcoesAuditoria.SinistroAprovado, AcoesAuditoria.SinistroPago]);
        ultimas[0].Detalhes.ShouldBeNull();
        ultimas[1].Detalhes.DeveSerJsonEquivalente("""{"valorIndenizacao":7500.00}""");
        ultimas[2].Detalhes.DeveSerJsonEquivalente("""{"valorPago":7500.00}""");
        Amb.Banco.Outbox.OfType<SinistroAprovado>().ShouldHaveSingleItem();
        Amb.Banco.Outbox.OfType<SinistroPago>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Deve_negar_com_motivo_no_historico_e_sem_motivo_na_auditoria_quando_admin_nega()
    {
        var sinistro = await SinistroAbertoAsync();
        Amb.Como(Amb.Diego);
        await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(sinistro.Id), Ct);

        var negado = (await Amb.Servico<NegarSinistroHandler>().HandleAsync(new NegarSinistroCommand(sinistro.Id, MotivoNegativa), Ct)).DeveTerSucesso();

        negado.Status.ShouldBe(StatusSinistro.Negado);
        negado.MotivoNegativa.ShouldBe(MotivoNegativa);
        Amb.Auditoria.Registros.Last().ShouldBe(new RegistroAuditoria(AcoesAuditoria.SinistroNegado, EntidadesAuditoria.Sinistro, sinistro.Id.ToString(), null));
    }

    [Fact]
    public async Task Deve_retornar_transicao_invalida_quando_pula_etapas()
    {
        var sinistro = await SinistroAbertoAsync();
        Amb.Como(Amb.Carla);

        var aprovarSemAnalise = await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(sinistro.Id, 100m, null), Ct);
        var pagarSemAprovar = await Amb.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(sinistro.Id), Ct);
        var negarSemAnalise = await Amb.Servico<NegarSinistroHandler>().HandleAsync(new NegarSinistroCommand(sinistro.Id, MotivoNegativa), Ct);

        aprovarSemAnalise.DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
        pagarSemAprovar.DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
        negarSemAnalise.DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public async Task Deve_recusar_transicao_repetida_quando_analisa_ou_paga_duas_vezes_ou_aprova_apos_negar()
    {
        var aprovado = await SinistroAbertoAsync();
        var negado = await Amb.AbrirSinistroAsync(Amb.Ana, Amb.Banco.Apolices.Single().Id, 2_000m);
        Amb.Como(Amb.Carla);
        (await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(aprovado.Id), Ct)).DeveTerSucesso();
        var analisarDeNovo = await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(aprovado.Id), Ct);
        (await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(aprovado.Id, 1_000m, null), Ct)).DeveTerSucesso();
        (await Amb.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(aprovado.Id), Ct)).DeveTerSucesso();
        var pagarDeNovo = await Amb.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(aprovado.Id), Ct);
        await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(negado.Id), Ct);
        (await Amb.Servico<NegarSinistroHandler>().HandleAsync(new NegarSinistroCommand(negado.Id, MotivoNegativa), Ct)).DeveTerSucesso();
        var savesAntes = Amb.Banco.TotalSaves;

        var aprovarAposNegar = await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(negado.Id, 100m, null), Ct);

        analisarDeNovo.DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
        pagarDeNovo.DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
        aprovarAposNegar.DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Banco.Outbox.OfType<SinistroPago>().ShouldHaveSingleItem();
        Amb.Banco.Sinistros.Single(s => s.Id == negado.Id).Status.ShouldBe(StatusSinistro.Negado);
    }

    [Fact]
    public async Task Deve_limitar_indenizacao_e_exigir_motivo_quando_decisao_invalida()
    {
        var sinistro = await SinistroAbertoAsync();
        Amb.Como(Amb.Carla);
        await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(sinistro.Id), Ct);

        var acima = await Amb.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(sinistro.Id, 8_000.01m, null), Ct);
        var motivoCurto = await Amb.Servico<NegarSinistroHandler>().HandleAsync(new NegarSinistroCommand(sinistro.Id, "curto"), Ct);

        acima.DeveFalharCom("Sinistro.IndenizacaoAcimaDoLimite", ErrorType.RegraDeNegocio);
        motivoCurto.DeveFalharCom("Sinistro.MotivoNegativaInvalido", ErrorType.Validacao);
    }

    [Fact]
    public async Task Deve_proibir_o_corretor_dono_quando_tenta_decidir_o_proprio_sinistro()
    {
        var sinistro = await SinistroAbertoAsync();
        Amb.Como(Amb.Ana);

        var resultado = await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(sinistro.Id), Ct);

        resultado.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        Amb.Banco.Sinistros.Single().Status.ShouldBe(StatusSinistro.Aberto);
    }

    [Fact]
    public async Task Deve_retornar_nao_encontrado_quando_sinistro_inexistente()
    {
        Amb.Como(Amb.Carla);

        var resultado = await Amb.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(Guid.NewGuid()), Ct);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
    }

    private async Task<SinistroDto> SinistroAbertoAsync()
    {
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        return await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
    }
}

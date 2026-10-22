using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Recusar;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;

namespace Apolice.Application.Tests.Cotacoes;

public sealed class TransicoesCotacaoTests : TesteBase
{
    [Fact]
    public async Task Deve_aceitar_e_auditar_quando_dono_aceita_cotacao_vigente()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);
        var savesAntes = Amb.Banco.TotalSaves;

        var aceita = (await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct)).DeveTerSucesso();

        aceita.Status.ShouldBe(StatusEfetivoCotacao.Aceita);
        Amb.Banco.Cotacoes.Single().Status.ShouldBe(StatusCotacao.Aceita);
        Amb.Auditoria.Acoes.ShouldBe([AcoesAuditoria.CotacaoCriada, AcoesAuditoria.CotacaoAceita]);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes + 1);
    }

    [Fact]
    public async Task Deve_recusar_quando_dono_recusa_mesmo_apos_aceite()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);

        var recusada = (await Amb.Servico<RecusarCotacaoHandler>().HandleAsync(new RecusarCotacaoCommand(cotacao.Id), Ct)).DeveTerSucesso();

        recusada.Status.ShouldBe(StatusEfetivoCotacao.Recusada);
        Amb.Auditoria.Acoes.Last().ShouldBe(AcoesAuditoria.CotacaoRecusada);
    }

    [Fact]
    public async Task Deve_retornar_expirada_quando_aceite_apos_30_dias()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);
        Amb.Avancar(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));
        var savesAntes = Amb.Banco.TotalSaves;

        var resultado = await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);

        resultado.DeveFalharCom(CotacaoErros.Expirada.Codigo, ErrorType.RegraDeNegocio);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
    }

    [Fact]
    public async Task Deve_retornar_status_invalido_quando_aceita_cotacao_ja_aceita()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);
        var savesAntes = Amb.Banco.TotalSaves;

        var aceite = await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);

        aceite.DeveFalharCom("Cotacao.StatusInvalido", ErrorType.RegraDeNegocio);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
    }

    [Fact]
    public async Task Deve_retornar_status_invalido_quando_aceita_ou_recusa_cotacao_recusada()
    {
        var cotacao = await Amb.CotacaoAceitaAsync(Amb.Ana);
        await Amb.Servico<RecusarCotacaoHandler>().HandleAsync(new RecusarCotacaoCommand(cotacao.Id), Ct);

        var aceite = await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);
        var recusa = await Amb.Servico<RecusarCotacaoHandler>().HandleAsync(new RecusarCotacaoCommand(cotacao.Id), Ct);

        aceite.DeveFalharCom("Cotacao.StatusInvalido", ErrorType.RegraDeNegocio);
        recusa.DeveFalharCom("Cotacao.StatusInvalido", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public async Task Deve_permitir_admin_e_proibir_analista_quando_operam_cotacao_alheia()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);

        Amb.Como(Amb.Carla);
        var analistaAceita = await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);
        var analistaRecusa = await Amb.Servico<RecusarCotacaoHandler>().HandleAsync(new RecusarCotacaoCommand(cotacao.Id), Ct);
        Amb.Como(Amb.Diego);
        var admin = await Amb.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(cotacao.Id), Ct);

        analistaAceita.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        analistaRecusa.DeveFalharCom("Comum.Proibido", ErrorType.Proibido);
        admin.DeveTerSucesso().Status.ShouldBe(StatusEfetivoCotacao.Aceita);
    }

    [Fact]
    public async Task Deve_retornar_nao_encontrado_quando_cotacao_inexistente()
    {
        var resultado = await Amb.Servico<RecusarCotacaoHandler>().HandleAsync(new RecusarCotacaoCommand(Guid.NewGuid()), Ct);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
    }
}

using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Comum;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Notificacoes;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Common;

// SPEC 9: CPF e e-mail completos nunca vão para log. Requests e DTOs são records, cujo ToString gerado lista tudo,
// e acabam em logs de validação, de idempotência ou em mensagens de exceção.
public sealed class DadosPessoaisEmTextoTests : TesteBase
{
    private const string CpfSoDigitos = "52998224725";

    [Fact]
    public void Deve_mascarar_cpf_e_email_quando_requisicao_de_cotacao_vira_texto()
    {
        var texto = Dados.CotacaoAuto().ToString();

        texto.ShouldNotContain(Dados.CpfValido);
        texto.ShouldNotContain(Dados.EmailSegurado);
        texto.ShouldContain("***.982.247-**");
        texto.ShouldContain("m***@exemplo.com");
    }

    [Fact]
    public async Task Deve_mascarar_cpf_e_email_quando_detalhes_de_cotacao_e_apolice_viram_texto()
    {
        var cotacao = await Amb.CriarCotacaoAsync(Amb.Ana);
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);

        foreach (var texto in new[] { cotacao.ToString(), apolice.ToString(), SeguradoDto.De(Amb.Banco.Apolices.Single().Segurado).ToString() })
        {
            texto.ShouldNotContain(Dados.CpfValido);
            texto.ShouldNotContain(CpfSoDigitos);
            texto.ShouldNotContain(Dados.EmailSegurado);
        }
    }

    [Fact]
    public void Deve_mascarar_o_email_quando_notificacao_e_usuario_viram_texto()
    {
        var notificacao = new NotificacaoDto(Guid.NewGuid(), TipoNotificacao.BoasVindas, Dados.EmailSegurado, "Bem-vindo(a)!", "Olá, Maria Silva!", DateTimeOffset.UnixEpoch);
        var usuario = new UsuarioDto(Guid.NewGuid(), "Ana Ribeiro", "ana.corretora@apolice.dev", Papel.Corretor);

        notificacao.ToString().ShouldNotContain(Dados.EmailSegurado);
        notificacao.ToString().ShouldNotContain("Olá, Maria Silva!");
        usuario.ToString().ShouldNotContain("ana.corretora@apolice.dev");
        usuario.ToString().ShouldContain("a***@apolice.dev");
    }

    [Fact]
    public async Task Deve_mascarar_quando_o_resultado_interno_da_tarifacao_vira_texto()
    {
        var tarifada = (await Amb.Servico<TarifacaoCotacao>().SimularAsync(Dados.CotacaoAuto(), Amb.Hoje, Ct)).DeveTerSucesso();

        tarifada.ToString().ShouldNotContain(Dados.EmailSegurado);
        tarifada.ToString().ShouldNotContain(CpfSoDigitos);
    }
}

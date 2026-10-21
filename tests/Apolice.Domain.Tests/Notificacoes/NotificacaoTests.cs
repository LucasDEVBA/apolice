using Apolice.Domain.Comum;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Notificacoes;

public sealed class NotificacaoTests
{
    private static readonly Guid ReferenciaId = Guid.Parse("0199a1b2-0000-7000-8000-000000000123");
    private static readonly Email Destinatario = Email.Criar("maria.silva@exemplo.com").Value;

    [Fact]
    public void Deve_criar_email_simulado_quando_dados_validos()
    {
        var notificacao = Notificacao.Criar(ReferenciaId, TipoNotificacao.BoasVindas, Destinatario, "  Bem-vinda!  ", "Sua apólice foi emitida.", Cenarios.Agora)
            .DeveTerSucesso();

        notificacao.Id.Version.ShouldBe(7);
        (notificacao.ReferenciaId, notificacao.Tipo, notificacao.Destinatario).ShouldBe((ReferenciaId, TipoNotificacao.BoasVindas, Destinatario));
        (notificacao.Assunto, notificacao.Corpo, notificacao.CriadaEm).ShouldBe(("Bem-vinda!", "Sua apólice foi emitida.", Cenarios.Agora));
    }

    [Fact]
    public void Deve_rejeitar_quando_assunto_ou_corpo_invalidos()
    {
        Criar(assunto: " ").DeveFalharCom("Notificacao.AssuntoInvalido", ErrorType.Validacao);
        Criar(assunto: new string('a', 201)).DeveFalharCom("Notificacao.AssuntoInvalido");
        Criar(corpo: "").DeveFalharCom("Notificacao.CorpoInvalido");
        Criar(corpo: new string('c', 10_001)).DeveFalharCom("Notificacao.CorpoInvalido");
    }

    [Fact]
    public void Deve_lancar_quando_referencia_tipo_ou_destinatario_invalidos_por_bug_do_consumidor()
    {
        Should.Throw<ArgumentException>(() => Criar(referencia: Guid.Empty));
        Should.Throw<ArgumentOutOfRangeException>(() => Criar(tipo: (TipoNotificacao)99));
        Should.Throw<ArgumentNullException>(() =>
            Notificacao.Criar(ReferenciaId, TipoNotificacao.Endosso, null!, "Assunto", "Corpo", Cenarios.Agora));
    }

    [Fact]
    public void Deve_aceitar_quando_assunto_e_corpo_no_tamanho_maximo()
    {
        Criar(assunto: new string('a', 200), corpo: new string('c', 10_000)).IsSuccess.ShouldBeTrue();
    }

    private static Result<Notificacao> Criar(
        Guid? referencia = null,
        TipoNotificacao tipo = TipoNotificacao.SinistroDecidido,
        string assunto = "Assunto",
        string corpo = "Corpo") =>
        Notificacao.Criar(referencia ?? ReferenciaId, tipo, Destinatario, assunto, corpo, Cenarios.Agora);
}

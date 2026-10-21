using Apolice.Domain.Comum;

namespace Apolice.Domain.Notificacoes;

public static class NotificacaoErros
{
    public static Error AssuntoInvalido(int maximo) =>
        Error.Validacao("Notificacao.AssuntoInvalido", $"O assunto é obrigatório e deve ter no máximo {maximo} caracteres.");

    public static Error CorpoInvalido(int maximo) =>
        Error.Validacao("Notificacao.CorpoInvalido", $"O corpo é obrigatório e deve ter no máximo {maximo} caracteres.");
}

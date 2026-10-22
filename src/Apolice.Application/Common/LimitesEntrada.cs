namespace Apolice.Application.Common;

// Tetos de tamanho dos validators (proteção do payload na borda), num lugar só. Os três primeiros espelham limites
// privados do domínio, que continua sendo quem decide: se ele expuser as constantes, estes valores passam a referenciá-las.
internal static class LimitesEntrada
{
    // CoberturaProduto.CodigoTamanhoMaximo.
    public const int CodigoCobertura = 30;

    // Sinistro: descrição, motivo da negativa e observação da aprovação.
    public const int TextoLivre = 2000;

    // Email.TamanhoMaximo (RFC 5321).
    public const int Email = 254;

    // Teto só de proteção: o PBKDF2 sobre um texto arbitrariamente longo vira vetor de negação de serviço.
    public const int Senha = 128;
}

using System.Text.RegularExpressions;

namespace Apolice.Domain.Comum;

public sealed partial record Email
{
    private const int TamanhoMaximo = 254;
    private const int TimeoutRegexMs = 100;

    private Email(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; }

    public static Result<Email> Criar(string? valor)
    {
        var normalizado = valor?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizado) || normalizado.Length > TamanhoMaximo || !FormatoEmail().IsMatch(normalizado))
        {
            return EmailErros.Invalido;
        }

        return new Email(normalizado);
    }

    // Porta de entrada do value converter do EF (mesmo motivo do Cpf: sem setter mapeável).
    public static Email Reconstituir(string valor)
    {
        var email = Criar(valor);
        if (email.IsFailure || email.Value.Valor != valor)
        {
            throw new DadoPersistidoInvalidoException("E-mail persistido inválido ou não normalizado.");
        }

        return email.Value;
    }

    public override string ToString() => Valor;

    // Validação deliberadamente simples: a prova real de um e-mail é a entrega, não a regex.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex FormatoEmail();
}

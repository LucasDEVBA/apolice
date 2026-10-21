namespace Apolice.Domain.Comum;

public sealed record Cpf
{
    private const int TotalDigitos = 11;
    private const int DigitosBase = 9;
    private const int Modulo = 11;
    private const int RestoQueZeraDigito = 2;

    private Cpf(string numero)
    {
        Numero = numero;
    }

    // Só dígitos: máscara é apresentação e não deve vazar para persistência nem comparação.
    public string Numero { get; }

    public string Formatado => $"{Numero[..3]}.{Numero[3..6]}.{Numero[6..9]}-{Numero[9..]}";

    public string Mascarado => $"***.{Numero[3..6]}.{Numero[6..9]}-**";

    public static Result<Cpf> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || !valor.All(CaracterePermitido))
        {
            return CpfErros.Invalido;
        }

        var digitos = new string([.. valor.Where(char.IsAsciiDigit)]);
        if (digitos.Length != TotalDigitos || digitos.Distinct().Count() == 1 || !DigitosVerificadoresConferem(digitos))
        {
            return CpfErros.Invalido;
        }

        return new Cpf(digitos);
    }

    // Porta de entrada do value converter do EF: sem setter mapeável, Cpf não serve como tipo complexo,
    // e é aqui que um valor gravado corrompido vira exceção em vez de VO inválido.
    public static Cpf Reconstituir(string numero)
    {
        var cpf = Criar(numero);
        if (cpf.IsFailure || cpf.Value.Numero != numero)
        {
            throw new DadoPersistidoInvalidoException("CPF persistido inválido ou fora do formato só-dígitos.");
        }

        return cpf.Value;
    }

    // ToString mascarado evita que um CPF completo vaze em logs por interpolação descuidada.
    public override string ToString() => Mascarado;

    private static bool CaracterePermitido(char caractere) =>
        char.IsAsciiDigit(caractere) || caractere is '.' or '-' or ' ';

    private static bool DigitosVerificadoresConferem(string digitos) =>
        CalcularDigito(digitos, DigitosBase) == digitos[DigitosBase] - '0'
        && CalcularDigito(digitos, DigitosBase + 1) == digitos[DigitosBase + 1] - '0';

    private static int CalcularDigito(string digitos, int quantidade)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * (quantidade + 1 - i);
        }

        var resto = soma % Modulo;
        return resto < RestoQueZeraDigito ? 0 : Modulo - resto;
    }
}

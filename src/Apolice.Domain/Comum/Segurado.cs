namespace Apolice.Domain.Comum;

public sealed record Segurado
{
    private const int NomeTamanhoMinimo = 3;
    private const int NomeTamanhoMaximo = 120;
    private static readonly DateOnly DataNascimentoMinima = new(1900, 1, 1);

    // Exigido pelo EF Core (tipo complexo); as propriedades são preenchidas pelo materializador.
    private Segurado()
    {
        Nome = null!;
        Cpf = null!;
        Email = null!;
    }

    private Segurado(string nome, Cpf cpf, Email email, DateOnly dataNascimento)
    {
        Nome = nome;
        Cpf = cpf;
        Email = email;
        DataNascimento = dataNascimento;
    }

    public string Nome { get; private init; }

    public Cpf Cpf { get; private init; }

    public Email Email { get; private init; }

    public DateOnly DataNascimento { get; private init; }

    public static Result<Segurado> Criar(string? nome, Cpf cpf, Email email, DateOnly dataNascimento)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentNullException.ThrowIfNull(email);

        var nomeNormalizado = nome?.Trim();
        if (nomeNormalizado is null || nomeNormalizado.Length is < NomeTamanhoMinimo or > NomeTamanhoMaximo)
        {
            return SeguradoErros.NomeInvalido(NomeTamanhoMinimo, NomeTamanhoMaximo);
        }

        if (dataNascimento < DataNascimentoMinima)
        {
            return SeguradoErros.DataNascimentoInvalida;
        }

        return new Segurado(nomeNormalizado, cpf, email, dataNascimento);
    }

    public static Result<Segurado> Criar(string? nome, string? cpf, string? email, DateOnly dataNascimento)
    {
        var cpfValidado = Cpf.Criar(cpf);
        if (cpfValidado.IsFailure)
        {
            return cpfValidado.Error;
        }

        var emailValidado = Email.Criar(email);
        if (emailValidado.IsFailure)
        {
            return emailValidado.Error;
        }

        return Criar(nome, cpfValidado.Value, emailValidado.Value, dataNascimento);
    }

    // Para leituras fora do EF (ex.: Dapper, importação): o EF materializa o tipo complexo pelo construtor
    // privado e pelos init, sem passar por aqui — nesse caminho só Cpf e Email (via converter) revalidam.
    public static Segurado Reconstituir(string nome, string cpf, string email, DateOnly dataNascimento)
    {
        var segurado = Criar(nome, Cpf.Reconstituir(cpf), Email.Reconstituir(email), dataNascimento);
        return segurado.IsSuccess
            ? segurado.Value
            : throw new DadoPersistidoInvalidoException($"Segurado persistido inválido: {segurado.Error.Codigo}.");
    }

    // AddYears trata o nascido em 29/02 como aniversariante em 28/02 nos anos não bissextos.
    public int IdadeEm(DateOnly data)
    {
        var idade = data.Year - DataNascimento.Year;
        return DataNascimento.AddYears(idade) > data ? idade - 1 : idade;
    }
}

namespace Apolice.Domain.Comum;

public sealed record Vigencia
{
    // Exigido pelo EF Core (tipo complexo); as propriedades são preenchidas pelo materializador.
    private Vigencia()
    {
    }

    private Vigencia(DateOnly inicio, DateOnly fim)
    {
        Inicio = inicio;
        Fim = fim;
    }

    public DateOnly Inicio { get; private init; }

    public DateOnly Fim { get; private init; }

    public int DiasTotais => Fim.DayNumber - Inicio.DayNumber;

    public static Result<Vigencia> Criar(DateOnly inicio, DateOnly fim) =>
        fim > inicio ? new Vigencia(inicio, fim) : VigenciaErros.Invalida;

    public static Vigencia Anual(DateOnly inicio) => new(inicio, inicio.AddYears(1));

    // Para leituras fora do EF; o EF materializa o tipo complexo pelos init privados, sem passar por aqui.
    public static Vigencia Reconstituir(DateOnly inicio, DateOnly fim)
    {
        var vigencia = Criar(inicio, fim);
        return vigencia.IsSuccess
            ? vigencia.Value
            : throw new DadoPersistidoInvalidoException("Vigência persistida com fim anterior ou igual ao início.");
    }

    // Início e fim inclusivos: o último dia ainda é coberto (a apólice só vence no dia seguinte).
    public bool Contem(DateOnly data) => data >= Inicio && data <= Fim;

    public int DiasDecorridos(DateOnly data) => Math.Clamp(data.DayNumber - Inicio.DayNumber, 0, DiasTotais);

    public int DiasRestantes(DateOnly data) => Math.Clamp(Fim.DayNumber - data.DayNumber, 0, DiasTotais);
}

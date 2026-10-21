namespace Apolice.Domain.Comum;

public static class Relogio
{
    private const int HorasOffsetBrasilia = -3;

    // O Brasil não tem horário de verão desde 2019; offset fixo dispensa tzdata (ausente no Lambda).
    public static readonly TimeSpan OffsetBrasilia = TimeSpan.FromHours(HorasOffsetBrasilia);

    public static DateOnly Hoje(TimeProvider tempo)
    {
        ArgumentNullException.ThrowIfNull(tempo);
        return HojeEm(tempo.GetUtcNow());
    }

    public static DateOnly HojeEm(DateTimeOffset instante) =>
        DateOnly.FromDateTime(instante.ToOffset(OffsetBrasilia).DateTime);
}

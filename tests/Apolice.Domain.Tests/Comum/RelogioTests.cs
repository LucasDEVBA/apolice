using Apolice.Domain.Comum;
using Microsoft.Extensions.Time.Testing;

namespace Apolice.Domain.Tests.Comum;

public sealed class RelogioTests
{
    [Fact]
    public void Deve_ser_ainda_o_dia_anterior_quando_utc_ja_virou_mas_brasilia_nao()
    {
        // 02:59:59Z = 23:59:59 do dia 10 em Brasília (−03:00).
        var tempo = new FakeTimeProvider(new DateTimeOffset(2026, 3, 11, 2, 59, 59, TimeSpan.Zero));

        Relogio.Hoje(tempo).ShouldBe(new DateOnly(2026, 3, 10));

        tempo.Advance(TimeSpan.FromSeconds(1)); // 03:00Z = meia-noite em Brasília
        Relogio.Hoje(tempo).ShouldBe(new DateOnly(2026, 3, 11));
    }

    [Fact]
    public void Deve_converter_para_brasilia_quando_instante_em_outro_fuso()
    {
        // 08:00 em +09:00 = 23:00Z do dia 10 = 20:00 do dia 10 em Brasília.
        Relogio.HojeEm(new DateTimeOffset(2026, 3, 11, 8, 0, 0, TimeSpan.FromHours(9))).ShouldBe(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void Deve_lancar_quando_time_provider_nulo()
    {
        Should.Throw<ArgumentNullException>(() => Relogio.Hoje(null!));
    }
}

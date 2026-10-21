using System.Text.Json;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;

namespace Apolice.Domain.Tests.Comum;

public sealed class EntidadeTests
{
    private static readonly Guid IdFixo = Guid.Parse("0199a1b2-0000-7000-8000-0000000000aa");
    private static readonly DateTimeOffset Instante = new(2026, 3, 10, 10, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Deve_ser_igual_quando_mesmo_tipo_e_mesmo_id()
    {
        var a = new EntidadeA(IdFixo);
        var b = new EntidadeA(IdFixo);

        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.Equals((object)b).ShouldBeTrue();
        a.Equals(a).ShouldBeTrue();
    }

    [Fact]
    public void Deve_ser_diferente_quando_tipo_ou_id_diferem()
    {
        var a = new EntidadeA(IdFixo);

        a.Equals(new EntidadeB(IdFixo)).ShouldBeFalse();
        (a == new EntidadeA(Guid.Parse("0199a1b2-0000-7000-8000-0000000000bb"))).ShouldBeFalse();
        a.Equals(null).ShouldBeFalse();
        (a == null).ShouldBeFalse();
        ((EntidadeA?)null == null).ShouldBeTrue();
    }

    [Fact]
    public void Deve_lancar_quando_id_nulo()
    {
        Should.Throw<ArgumentNullException>(() => new EntidadePorTexto(null!));
    }

    [Fact]
    public void Deve_acumular_e_limpar_eventos_quando_agregado_levanta_eventos()
    {
        var agregado = new AgregadoTeste(IdFixo);

        agregado.Levantar(new EventoTeste(Instante));
        agregado.Levantar(new EventoTeste(Instante));

        agregado.DomainEvents.Count.ShouldBe(2);
        agregado.Versao.ShouldBe(0); // só a infraestrutura incrementa ao salvar
        agregado.ClearDomainEvents();
        agregado.DomainEvents.ShouldBeEmpty();
        Should.Throw<ArgumentNullException>(() => agregado.Levantar(null!));
    }

    [Fact]
    public void Deve_gerar_id_v7_e_guardar_instante_quando_evento_criado()
    {
        var evento = new EventoTeste(Instante);

        evento.OcorridoEm.ShouldBe(Instante);
        evento.EventoId.Version.ShouldBe(7);
    }

    [Fact]
    public void Deve_preservar_o_evento_id_quando_desserializa_o_evento()
    {
        var original = new ApoliceEmitida(
            IdFixo, "AUTO-2026-000042", IdFixo, "AUTO", "Maria Silva", "maria@exemplo.com", 4_080.44m,
            new DateOnly(2026, 3, 10), new DateOnly(2027, 3, 10), Instante);

        var copia = JsonSerializer.Deserialize<ApoliceEmitida>(JsonSerializer.Serialize(original))!;

        copia.EventoId.ShouldBe(original.EventoId);
        copia.ShouldBe(original);
    }

    [Fact]
    public void Deve_esconder_o_setter_quando_identidade_do_evento()
    {
        // Só o próprio evento (e a desserialização via JsonInclude) define o Id: "with { EventoId = ... }" não compila fora.
        typeof(DomainEvent).GetProperty(nameof(DomainEvent.EventoId))!.SetMethod!.IsPublic.ShouldBeFalse();
    }

    private sealed class EntidadeA(Guid id) : Entity<Guid>(id);

    private sealed class EntidadeB(Guid id) : Entity<Guid>(id);

    private sealed class EntidadePorTexto(string id) : Entity<string>(id);

    private sealed class AgregadoTeste(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Levantar(IDomainEvent evento) => Raise(evento);
    }

    private sealed record EventoTeste(DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
}

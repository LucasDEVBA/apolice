using Apolice.Application.Abstractions.Messaging;
using Apolice.ArchitectureTests.Suporte;
using Apolice.Domain.Comum;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Apolice.ArchitectureTests;

public sealed class ConvencoesTests
{
    private const string SufixoHandler = "Handler";

    // SPEC §3. Um caso por contrato: com Or() entre os três, um contrato sem nenhuma implementação
    // encontrada passaria despercebido.
    [Theory]
    [InlineData(typeof(ICommandHandler<,>))]
    [InlineData(typeof(IQueryHandler<,>))]
    [InlineData(typeof(IIntegrationEventHandler<>))]
    public void Handlers_devem_ser_sealed_e_terminar_em_Handler(Type contrato) =>
        Classes().That().ImplementInterface(contrato)
            .Should().BeSealed()
            .AndShould().HaveNameEndingWith(SufixoHandler)
            .DeveSerRespeitada();

    // SPEC §4: o estado do agregado só muda pelos métodos de domínio. Setter "init" público também conta.
    [Fact]
    public void Agregados_nao_devem_ter_setters_publicos() =>
        PropertyMembers().That().AreDeclaredIn(Classes().That().AreAssignableTo(typeof(AggregateRoot<>)))
            .Should().NotHavePublicSetter()
            .DeveSerRespeitada();
}

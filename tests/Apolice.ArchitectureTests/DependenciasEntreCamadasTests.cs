using Apolice.ArchitectureTests.Suporte;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Apolice.ArchitectureTests;

// SPEC §3 — regras de dependência entre as camadas.
public sealed class DependenciasEntreCamadasTests
{
    [Theory]
    [InlineData("Apolice.Application")]
    [InlineData("Apolice.Infrastructure")]
    [InlineData("Apolice.Api")]
    [InlineData("Apolice.Worker")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("FluentValidation")]
    public void Domain_nao_deve_depender_de(string alvo) =>
        Types().That().ResideInAssembly(Solucao.Domain)
            .Should().NotDependOnAny(Solucao.Alvo(alvo))
            .DeveSerRespeitada();

    [Theory]
    [InlineData("Apolice.Infrastructure")]
    [InlineData("Apolice.Api")]
    [InlineData("Apolice.Worker")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Dapper")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Amazon")]
    [InlineData("RabbitMQ")]
    public void Application_nao_deve_depender_de(string alvo) =>
        Types().That().ResideInAssembly(Solucao.Application)
            .Should().NotDependOnAny(Solucao.Alvo(alvo))
            .DeveSerRespeitada();

    [Theory]
    [InlineData("Apolice.Api")]
    [InlineData("Apolice.Worker")]
    public void Infrastructure_nao_deve_depender_de(string alvo) =>
        Types().That().ResideInAssembly(Solucao.Infrastructure)
            .Should().NotDependOnAny(Solucao.Alvo(alvo))
            .DeveSerRespeitada();

    // Controle positivo: se um alvo não detectasse dependência nenhuma (namespace digitado errado, biblioteca
    // fora dos tipos referenciados), as regras acima passariam sem verificar nada.
    [Theory]
    [InlineData("Apolice.Infrastructure", "Apolice.Application")]
    [InlineData("Apolice.Api", "Apolice.Infrastructure")]
    [InlineData("Apolice.Worker", "Apolice.Infrastructure")]
    [InlineData("Apolice.Infrastructure", "Microsoft.EntityFrameworkCore")]
    [InlineData("Apolice.Infrastructure", "Npgsql")]
    [InlineData("Apolice.Infrastructure", "Dapper")]
    [InlineData("Apolice.Infrastructure", "Amazon")]
    [InlineData("Apolice.Infrastructure", "RabbitMQ")]
    [InlineData("Apolice.Api", "Microsoft.AspNetCore")]
    [InlineData("Apolice.Application", "FluentValidation")]
    public void Deve_detectar_a_dependencia_quando_a_camada_permitida_usa_o_alvo(string camada, string alvo) =>
        Types().That().ResideInAssembly(Solucao.Projeto(camada)).And().DependOnAny(Solucao.Alvo(alvo))
            .GetObjects(Solucao.Arquitetura)
            .ShouldNotBeEmpty();
}

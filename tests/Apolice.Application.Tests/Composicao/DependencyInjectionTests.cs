using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Eventos;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Apolice.Application.Tests.Composicao;

public sealed class DependencyInjectionTests : TesteBase
{
    private static readonly Type[] Contratos = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>), typeof(IIntegrationEventHandler<>)];

    public static TheoryData<Type> Handlers => [.. typeof(DependencyInjection).Assembly.GetTypes().Where(ImplementaHandler)];

    [Fact]
    public void Deve_encontrar_todos_os_casos_de_uso_quando_varre_o_assembly()
    {
        // 32 casos de uso (todas as rotas do SPEC 7.1) + 8 consumidores de evento: canário para handler esquecido.
        Handlers.Count.ShouldBe(40);
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public void Deve_ser_sealed_terminar_em_handler_e_resolver_pela_di_quando_implementa_contrato(Type handler)
    {
        handler.IsSealed.ShouldBeTrue();
        handler.Name.ShouldEndWith("Handler");
        Amb.Servico<IServiceProvider>().GetRequiredService(handler).ShouldBeOfType(handler);
        foreach (var contrato in handler.GetInterfaces().Where(i => i.IsGenericType && Contratos.Contains(i.GetGenericTypeDefinition())))
        {
            Amb.Servico<IServiceProvider>().GetServices(contrato).ShouldContain(s => s!.GetType() == handler);
        }
    }

    [Fact]
    public void Deve_nao_duplicar_registros_quando_add_application_chamado_duas_vezes()
    {
        var servicos = new ServiceCollection();
        servicos.AddApplication();
        var total = servicos.Count;

        servicos.AddApplication();

        servicos.Count.ShouldBe(total);
        servicos.Count(d => d.ServiceType == typeof(IIntegrationEventHandler<SinistroAberto>)).ShouldBe(2);
        Should.Throw<ArgumentNullException>(() => DependencyInjection.AddApplication(null!));
    }

    [Fact]
    public void Deve_respeitar_time_provider_existente_e_usar_o_do_sistema_quando_ausente()
    {
        var fake = new FakeTimeProvider();
        using var comFake = new ServiceCollection().AddSingleton<TimeProvider>(fake).AddApplication().BuildServiceProvider();
        using var padrao = new ServiceCollection().AddApplication().BuildServiceProvider();

        comFake.GetRequiredService<TimeProvider>().ShouldBeSameAs(fake);
        padrao.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void Deve_compor_o_tarifador_com_as_tres_estrategias_quando_resolvido()
    {
        var tarifador = Amb.Servico<ITarifador>();
        var segurado = Segurado.Criar("Maria Silva", Dados.CpfValido, Dados.EmailSegurado, new DateOnly(1985, 5, 20)).Value;

        tarifador.ShouldBeOfType<Tarifador>();
        Amb.Servico<IEnumerable<IEstrategiaTarifacao>>().Select(e => e.ProdutoCodigo).Order(StringComparer.Ordinal).ShouldBe(["AUTO", "RESID", "VIDA"]);
        tarifador.Calcular(CatalogoPadrao.Vida(), segurado, new DadosRiscoVida(false), [new(CodigosCobertura.Morte, 100_000m)], new DateOnly(2026, 3, 10))
            .DeveTerSucesso().PremioLiquido.ShouldBe(120.00m);
    }

    [Fact]
    public void Deve_registrar_validators_e_dispatcher_quando_compoe()
    {
        Amb.Servico<IValidator<LoginRequest>>().ShouldBeOfType<LoginRequestValidator>();
        Amb.Servico<IValidator<CotacaoRequest>>().ShouldBeOfType<CotacaoRequestValidator>();
        Amb.Servico<IIntegrationEventDispatcher>().ShouldBeOfType<IntegrationEventDispatcher>();
    }

    // Guarda da regra do SPEC 3 até o projeto de testes de arquitetura (ArchUnitNET) existir: a aplicação não pode
    // depender de infraestrutura, banco, ASP.NET, AWS nem broker.
    [Fact]
    public void Deve_referenciar_so_dominio_e_abstracoes_quando_compila_a_aplicacao()
    {
        string[] proibidos = ["Apolice.Infrastructure", "Apolice.Api", "Apolice.Worker", "Microsoft.EntityFrameworkCore", "Npgsql", "Dapper", "Microsoft.AspNetCore", "AWSSDK", "Amazon", "RabbitMQ"];

        var referencias = typeof(DependencyInjection).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        referencias.ShouldContain("Apolice.Domain");
        referencias.ShouldNotContain(r => proibidos.Any(p => r.StartsWith(p, StringComparison.Ordinal)));
    }

    private static bool ImplementaHandler(Type tipo) =>
        tipo is { IsClass: true, IsAbstract: false }
        && tipo.GetInterfaces().Any(i => i.IsGenericType && Contratos.Contains(i.GetGenericTypeDefinition()));
}

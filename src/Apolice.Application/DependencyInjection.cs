using System.Reflection;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Eventos;
using Apolice.Application.Features.Sinistros;
using Apolice.Domain.Tarifacao;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apolice.Application;

public static class DependencyInjection
{
    private static readonly Type[] InterfacesDeHandler =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IIntegrationEventHandler<>),
    ];

    // Idempotente (TryAdd*): chamar duas vezes não duplica handlers nem consumidores de evento.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Singleton);
        RegistrarTarifacao(services);
        RegistrarServicos(services);
        RegistrarHandlers(services, assembly);
        services.TryAddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
        return services;
    }

    // Estratégias sem estado: singletons. O Tarifador recebe todas pelo IEnumerable (Strategy do SPEC 4.2).
    private static void RegistrarTarifacao(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEstrategiaTarifacao, TarifacaoAuto>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEstrategiaTarifacao, TarifacaoResidencial>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEstrategiaTarifacao, TarifacaoVida>());
        services.TryAddSingleton<ITarifador, Tarifador>();
    }

    private static void RegistrarServicos(IServiceCollection services)
    {
        services.TryAddScoped<EmissorSessao>();
        services.TryAddScoped<RevogacaoSessao>();
        services.TryAddScoped<TarifacaoCotacao>();
        services.TryAddScoped<TransicaoCotacao>();
        services.TryAddScoped<AcessoApolice>();
        services.TryAddScoped<PreparacaoEndosso>();
        services.TryAddScoped<AcaoSinistro>();
    }

    // Registra o tipo concreto (endpoints podem injetar o handler direto) e cada interface fechada
    // (o dispatcher resolve IEnumerable<IIntegrationEventHandler<T>>).
    private static void RegistrarHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var tipo in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            var interfaces = tipo.GetInterfaces()
                .Where(i => i.IsGenericType && InterfacesDeHandler.Contains(i.GetGenericTypeDefinition()))
                .ToList();
            if (interfaces.Count == 0)
            {
                continue;
            }

            services.TryAddScoped(tipo);
            foreach (var contrato in interfaces)
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(contrato, tipo));
            }
        }
    }
}

using Amazon.SQS;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Infrastructure.Configuracao;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Idempotencia;
using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Messaging.RabbitMq;
using Apolice.Infrastructure.Messaging.Sqs;
using Apolice.Infrastructure.Outbox;
using Apolice.Infrastructure.Persistence;
using Apolice.Infrastructure.Persistence.Interceptors;
using Apolice.Infrastructure.Persistence.Migracao;
using Apolice.Infrastructure.Persistence.Provedores;
using Apolice.Infrastructure.Persistence.Queries;
using Apolice.Infrastructure.Persistence.Repositories;
using Apolice.Infrastructure.Persistence.Seed;
using Apolice.Infrastructure.Saude;
using Apolice.Infrastructure.Security;
using Apolice.Infrastructure.Services;
using Apolice.Infrastructure.Services.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Npgsql;

namespace Apolice.Infrastructure;

public static class DependencyInjection
{
    public const string TagProntidao = "ready";
    private const int TentativasBanco = 5;
    private static readonly TimeSpan EsperaMaximaBanco = TimeSpan.FromSeconds(2);

    // Composição de todas as portas da Application. Options críticas validadas no start (ValidateOnStart): uma
    // configuração errada derruba o host na subida, não na primeira requisição. Quem hospeda (API/Worker) ainda
    // registra: ICurrentUser real (a API; o padrão aqui é "sistema"), o preenchimento do ContextoCorrelacao e os hosted
    // services (relay do outbox e RabbitMqConsumidor).
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment ambiente)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(ambiente);

        services.TryAddSingleton(TimeProvider.System);

        // O host já registra o ambiente; o TryAdd só cobre composições sem host (testes, ferramentas).
        services.TryAddSingleton(ambiente);
        RegistrarOpcoes(services, configuration);
        RegistrarPersistencia(services);
        RegistrarLeitura(services);
        RegistrarMensageria(services, configuration);
        RegistrarSeguranca(services);
        RegistrarServicos(services, configuration);
        RegistrarSaude(services, configuration);
        return services;
    }

    // Para a API: Jwt:SigningKey ausente ou curta (fora de Development) derruba a subida, não o primeiro login.
    public static IServiceCollection ExigirJwtNaInicializacao(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<JwtOptions>().ValidateOnStart();
        return services;
    }

    private static void RegistrarOpcoes(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.Secao))
            .Configure(o => o.ConnectionString ??= configuration.GetConnectionString(DatabaseOptions.NomeConnectionString))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseOptions>, ValidacaoDatabaseOptions>());

        services.AddOptions<MessagingOptions>().Bind(configuration.GetSection(MessagingOptions.Secao)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MessagingOptions>, ValidacaoMessagingOptions>());

        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.Secao)).ValidateDataAnnotations().ValidateOnStart();

        // Sem ValidateOnStart aqui: só a API emite e valida tokens. O Worker (consumidor, relay, migrate, seed) roda no
        // deploy sem Jwt:SigningKey e não pode cair por uma chave que não usa; a API chama ExigirJwtNaInicializacao.
        // Em qualquer host, o primeiro uso das options (ITokenService) ainda valida a chave.
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.Secao));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtOptions>, ChaveEfemeraJwtDevelopment>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JwtOptions>, ValidacaoJwtOptions>());

        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.Secao));
    }

    private static void RegistrarPersistencia(IServiceCollection services)
    {
        services.TryAddSingleton(sp => FonteDadosNpgsql.Criar(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value, sp.GetRequiredService<ILoggerFactory>()));
        services.TryAddScoped<ContextoCorrelacao>();
        services.AddDbContext<ApoliceDbContext>(ConfigurarDbContext);
        services.TryAddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApoliceDbContext>());

        services.TryAddScoped<ICotacaoRepository, CotacaoRepository>();
        services.TryAddScoped<IApoliceRepository, ApoliceRepository>();
        services.TryAddScoped<ISinistroRepository, SinistroRepository>();
        services.TryAddScoped<IProdutoRepository, ProdutoRepository>();
        services.TryAddScoped<IUsuarioRepository, UsuarioRepository>();
        services.TryAddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.TryAddScoped<INotificacaoRepository, NotificacaoRepository>();
        services.TryAddScoped<IMensagensProcessadas, MensagensProcessadas>();
        services.TryAddSingleton<INumerador, Numerador>();

        services.TryAddScoped<DsqlMigrationRunner>();
        services.TryAddScoped<IMigradorBanco, MigradorBanco>();
        services.TryAddScoped<DataSeeder>();
        services.TryAddSingleton<IArmazemIdempotencia, ArmazemIdempotencia>();
    }

    // Interceptors novos por DbContext: o de outbox lê o ContextoCorrelacao do escopo e o despacho imediato guarda o
    // que o save gravou. Retry do EF só para erros transitórios + conflitos de OCC do DSQL (40001/OC000/OC001): um
    // SaveChanges inteiro é refeito numa transação nova, e a WHERE de versão continua protegendo contra sobrescrita.
    private static void ConfigurarDbContext(IServiceProvider sp, DbContextOptionsBuilder opcoes)
    {
        opcoes.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql
                .MigrationsAssembly(typeof(ApoliceDbContext).Assembly.GetName().Name)
                .EnableRetryOnFailure(TentativasBanco, EsperaMaximaBanco, [.. ErrosTransitorios.CodigosOcc]))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new DomainEventsToOutboxInterceptor(sp.GetRequiredService<ContextoCorrelacao>()), new OrdemFilhosInterceptor(), new VersaoInterceptor());

        if (sp.GetRequiredService<IOptions<OutboxOptions>>().Value.DispatchImediato)
        {
            opcoes.AddInterceptors(ActivatorUtilities.CreateInstance<OutboxDispatchImediatoInterceptor>(sp));
        }
    }

    private static void RegistrarLeitura(IServiceCollection services)
    {
        DapperConfiguracao.Garantir();
        services.TryAddScoped<ICotacaoQueries, CotacaoQueries>();
        services.TryAddScoped<IApoliceQueries, ApoliceQueries>();
        services.TryAddScoped<ISinistroQueries, SinistroQueries>();
        services.TryAddScoped<IDashboardQueries, DashboardQueries>();
        services.TryAddScoped<IAuditoriaQueries, AuditoriaQueries>();
    }

    // O transporte decide o que é registrado (não só qual implementação): sem RabbitMQ configurado, nenhuma conexão
    // AMQP é sequer construída; sem SQS, nenhum cliente AWS.
    private static void RegistrarMensageria(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<ProcessadorMensagemIntegracao>();
        services.TryAddScoped<OutboxDispatcher>();
        switch (Transporte(configuration))
        {
            case TransporteMensageria.RabbitMq:
                services.TryAddSingleton<ConexaoRabbitMq>();
                services.TryAddSingleton<IMessageBus, RabbitMqMessageBus>();
                services.TryAddSingleton<RabbitMqConsumidor>();
                break;
            case TransporteMensageria.Sqs:
                services.TryAddAWSService<IAmazonSQS>();
                services.TryAddSingleton<IMessageBus, SqsMessageBus>();
                break;
            default:
                services.TryAddSingleton<IMessageBus, InMemoryMessageBus>();
                break;
        }
    }

    private static void RegistrarSeguranca(IServiceCollection services)
    {
        services.TryAddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.TryAddSingleton<ITokenService, JwtTokenService>();
        services.TryAddScoped<ICurrentUser, UsuarioSistema>();
    }

    private static void RegistrarServicos(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHybridCache();
        if (configuration["Redis:ConnectionString"] is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "apolice:";
            });
        }

        services.TryAddSingleton<ICatalogoProdutos, CatalogoProdutosCache>();
        services.AddFeatureManagement(ComPadroesDeFeatures(configuration));
        services.TryAddSingleton<IFeatureFlags, FeatureFlagsAdapter>();
        services.TryAddScoped<IAuditoria, Auditoria>();
        services.TryAddSingleton<IGeradorDocumentoApolice, QuestPdfGeradorDocumentoApolice>();
    }

    // SPEC 13: as flags nascem ligadas. O FeatureManagement trata flag ausente como desligada, então os padrões entram
    // como a camada mais baixa da configuração e qualquer valor explícito (appsettings, env, SSM) prevalece.
    private static IConfiguration ComPadroesDeFeatures(IConfiguration configuration) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(FeatureFlagsPadrao().ToDictionary(f => $"FeatureManagement:{f}", _ => (string?)"true"))
            .AddConfiguration(configuration)
            .Build();

    private static IEnumerable<string> FeatureFlagsPadrao() =>
        [FeatureFlags.LoginDemo, FeatureFlags.TriagemAutomaticaSinistros, FeatureFlags.DocumentoPdf, FeatureFlags.EndossoOnline];

    // /health/ready (tag "ready"): banco sempre; broker conforme o transporte (InMemory não tem dependência externa).
    private static void RegistrarSaude(IServiceCollection services, IConfiguration configuration)
    {
        var saude = services.AddHealthChecks()
            .AddNpgSql(sp => sp.GetRequiredService<NpgsqlDataSource>(), name: "banco", tags: [TagProntidao]);

        switch (Transporte(configuration))
        {
            case TransporteMensageria.RabbitMq:
                saude.AddRabbitMQ(sp => sp.GetRequiredService<ConexaoRabbitMq>().ObterAsync(CancellationToken.None), name: "rabbitmq", tags: [TagProntidao]);
                break;
            case TransporteMensageria.Sqs:
                saude.AddCheck<SqsHealthCheck>("sqs", tags: [TagProntidao]);
                break;
            default:
                break;
        }
    }

    private static TransporteMensageria Transporte(IConfiguration configuration) =>
        configuration.GetSection(MessagingOptions.Secao).GetValue<TransporteMensageria?>(nameof(MessagingOptions.Transport)) ?? TransporteMensageria.RabbitMq;
}

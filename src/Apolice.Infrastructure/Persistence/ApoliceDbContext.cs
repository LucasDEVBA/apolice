using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Outbox;
using Apolice.Infrastructure.Persistence.Conversores;
using Apolice.Infrastructure.Persistence.Provedores;
using Apolice.Infrastructure.Persistence.Registros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apolice.Infrastructure.Persistence;

public sealed class ApoliceDbContext(DbContextOptions<ApoliceDbContext> options) : DbContext(options), IUnitOfWork
{
    internal const string PropriedadeVersao = nameof(AggregateRoot<Guid>.Versao);
    internal const string PropriedadeOrdem = "Ordem";
    private const string PropriedadeEventos = nameof(AggregateRoot<Guid>.DomainEvents);
    private const int TamanhoEnum = 30;
    private const int PrecisaoDinheiro = 14;
    private const int EscalaDinheiro = 2;

    internal DbSet<Usuario> Usuarios => Set<Usuario>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    internal DbSet<Produto> Produtos => Set<Produto>();

    internal DbSet<Cotacao> Cotacoes => Set<Cotacao>();

    internal DbSet<ApoliceSeguro> Apolices => Set<ApoliceSeguro>();

    internal DbSet<Sinistro> Sinistros => Set<Sinistro>();

    internal DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    internal DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    internal DbSet<MensagemProcessada> MensagensProcessadas => Set<MensagemProcessada>();

    internal DbSet<AuditoriaRegistro> Auditoria => Set<AuditoriaRegistro>();

    internal DbSet<IdempotenciaRegistro> Idempotencia => Set<IdempotenciaRegistro>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (Exception ex) when (EhConflito(ex))
        {
            throw new ConflitoDeConcorrenciaException("O registro foi alterado por outra operação.", ex);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (Exception ex) when (EhConflito(ex))
        {
            throw new ConflitoDeConcorrenciaException("O registro foi alterado por outra operação.", ex);
        }
    }

    // Tudo o que o caso de uso deixou pendente (agregados, outbox, inbox, auditoria) vive no ChangeTracker.
    public void DescartarAlteracoes() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApoliceDbContext).Assembly);
        ConfigurarAgregados(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Cpf>().HaveConversion<CpfConverter>().HaveMaxLength(11);
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter>().HaveMaxLength(254);
        configurationBuilder.Properties<Dinheiro>().HaveConversion<DinheiroConverter>().HavePrecision(PrecisaoDinheiro, EscalaDinheiro);
        configurationBuilder.Properties<DadosRisco>().HaveConversion<DadosRiscoConverter>().HaveColumnType("text");
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();

        // Padrão = dinheiro numeric(14,2); taxas, alíquotas e fatores sobrescrevem para numeric(12,6) na configuração.
        configurationBuilder.Properties<decimal>().HavePrecision(PrecisaoDinheiro, EscalaDinheiro);

        // Enums como texto: o SQL do lado de leitura (Dapper) filtra e devolve o mesmo nome que a API expõe.
        EnumComoTexto<StatusCotacao>(configurationBuilder);
        EnumComoTexto<StatusApolice>(configurationBuilder);
        EnumComoTexto<MotivoCancelamento>(configurationBuilder);
        EnumComoTexto<TipoEndosso>(configurationBuilder);
        EnumComoTexto<StatusSinistro>(configurationBuilder);
        EnumComoTexto<TipoNotificacao>(configurationBuilder);
        EnumComoTexto<Papel>(configurationBuilder);
        EnumComoTexto<TipoCalculo>(configurationBuilder);
    }

    private static void EnumComoTexto<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum =>
        configurationBuilder.Properties<TEnum>().HaveConversion<string>().HaveMaxLength(TamanhoEnum);

    // Regras comuns a todo agregado: Versao é o token de concorrência otimista e a lista de eventos não é persistida
    // (os eventos viram linhas do outbox). Ids do domínio nunca são gerados pelo banco (o DSQL não tem sequence).
    private static void ConfigurarAgregados(ModelBuilder modelBuilder)
    {
        foreach (var tipo in modelBuilder.Model.GetEntityTypes().Where(t => EhAgregado(t.ClrType)).ToList())
        {
            var entidade = modelBuilder.Entity(tipo.ClrType);
            entidade.Ignore(PropriedadeEventos);
            entidade.Property(PropriedadeVersao).IsConcurrencyToken();
            entidade.Property("Id").ValueGeneratedNever();
        }
    }

    internal static bool EhAgregado(Type tipo) => BaseGenerica(tipo, typeof(AggregateRoot<>));

    internal static bool EhAgregado(IReadOnlyEntityType tipo) => EhAgregado(tipo.ClrType);

    private static bool BaseGenerica(Type? tipo, Type definicao)
    {
        for (var atual = tipo; atual is not null; atual = atual.BaseType)
        {
            if (atual.IsGenericType && atual.GetGenericTypeDefinition() == definicao)
            {
                return true;
            }
        }

        return false;
    }

    // Conflito de versão (0 linhas afetadas) ou, no DSQL, conflito de OCC que persistiu além das retentativas:
    // nos dois casos outra transação gravou o mesmo registro primeiro.
    private static bool EhConflito(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || (ex is RetryLimitExceededException && ErrosTransitorios.EhConflitoOcc(ex.InnerException));
}

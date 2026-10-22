using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Apolice.Infrastructure.Configuracao;

public enum ProvedorBanco
{
    Postgres,
    Dsql,
}

// Seção "Database" do SPEC 13; a connection string vem de ConnectionStrings:Apolice (só Postgres).
public sealed class DatabaseOptions
{
    public const string Secao = "Database";
    public const string NomeConnectionString = "Apolice";

    public ProvedorBanco Provider { get; set; } = ProvedorBanco.Postgres;

    public string? ConnectionString { get; set; }

    public string? DsqlEndpoint { get; set; }

    // "admin" no migrator/seed (DbConnectAdmin) e "apolice_app" nas funções (DbConnect, privilégio mínimo).
    public string DsqlUser { get; set; } = UsuarioAdminDsql;

    public string AppRole { get; set; } = "apolice_app";

    public IList<string> AppRoleArns { get; } = new List<string>();

    public const string UsuarioAdminDsql = "admin";

    public bool UsuarioAdmin => string.Equals(DsqlUser, UsuarioAdminDsql, StringComparison.Ordinal);
}

internal sealed partial class ValidacaoDatabaseOptions : IValidateOptions<DatabaseOptions>
{
    private const int TimeoutRegexMs = 100;

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Provider == ProvedorBanco.Postgres)
        {
            return string.IsNullOrWhiteSpace(options.ConnectionString)
                ? ValidateOptionsResult.Fail($"ConnectionStrings:{DatabaseOptions.NomeConnectionString} é obrigatória com Database:Provider=Postgres.")
                : ValidateOptionsResult.Success;
        }

        var falhas = new List<string>();
        if (string.IsNullOrWhiteSpace(options.DsqlEndpoint) || !EndpointDsql().IsMatch(options.DsqlEndpoint))
        {
            falhas.Add("Database:DsqlEndpoint deve ser o endpoint do cluster (<id>.dsql.<região>.on.aws).");
        }

        if (!IdentificadorSql().IsMatch(options.DsqlUser ?? string.Empty))
        {
            falhas.Add("Database:DsqlUser inválido.");
        }

        // Nome do papel e ARNs entram em DDL (CREATE ROLE / AWS IAM GRANT), que não aceita parâmetro: o formato
        // estrito aqui é a defesa contra injeção no migrator.
        if (!IdentificadorSql().IsMatch(options.AppRole ?? string.Empty))
        {
            falhas.Add("Database:AppRole deve ser um identificador simples (minúsculas, dígitos e _).");
        }

        falhas.AddRange(options.AppRoleArns.Where(arn => !ArnIam().IsMatch(arn)).Select(arn => $"Database:AppRoleArns contém um ARN inválido: '{arn}'."));
        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    [GeneratedRegex(@"^[a-z0-9]+\.dsql(-[a-z0-9]+)?\.[a-z0-9-]+\.on\.aws$", RegexOptions.CultureInvariant, TimeoutRegexMs)]
    private static partial Regex EndpointDsql();

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant, TimeoutRegexMs)]
    internal static partial Regex IdentificadorSql();

    [GeneratedRegex(@"^arn:aws[a-z-]*:iam::\d{12}:(role|user)/[A-Za-z0-9+=,.@_/-]{1,512}$", RegexOptions.CultureInvariant, TimeoutRegexMs)]
    internal static partial Regex ArnIam();
}

using Npgsql;

namespace Apolice.Infrastructure.Persistence.Provedores;

// Códigos de conflito de concorrência otimista: 40001 é o serialization_failure do PostgreSQL; OC000 (dados) e
// OC001 (schema) são os próprios do Aurora DSQL, que detecta o conflito no COMMIT em vez de bloquear.
internal static class ErrosTransitorios
{
    public const string SerializacaoFalhou = "40001";
    public const string ConflitoOccDados = "OC000";
    public const string ConflitoOccSchema = "OC001";

    public static IReadOnlyList<string> CodigosOcc { get; } = [SerializacaoFalhou, ConflitoOccDados, ConflitoOccSchema];

    public static bool EhConflitoOcc(Exception? ex)
    {
        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is PostgresException { SqlState: var codigo } && CodigosOcc.Contains(codigo))
            {
                return true;
            }
        }

        return false;
    }
}

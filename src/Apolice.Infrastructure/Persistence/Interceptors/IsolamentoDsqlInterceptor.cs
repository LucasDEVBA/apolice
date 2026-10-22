using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apolice.Infrastructure.Persistence.Interceptors;

// Só no Aurora DSQL. O SaveChanges com vários comandos abre a transação com IsolationLevel.Unspecified, que o Npgsql
// envia como "BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED"; o DSQL só oferece o equivalente ao REPEATABLE READ
// (snapshot + OCC no commit). Pedir o nível que o serviço de fato aplica tira do caminho de TODA escrita a dúvida de
// como ele trata um nível que não tem (o comando não está na lista verificada da SPEC 14).
internal sealed class IsolamentoDsqlInterceptor : DbTransactionInterceptor
{
    private const IsolationLevel NivelDsql = IsolationLevel.RepeatableRead;

    public override InterceptionResult<DbTransaction> TransactionStarting(
        DbConnection connection,
        TransactionStartingEventData eventData,
        InterceptionResult<DbTransaction> result)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(eventData);
        return Ajustavel(eventData, result)
            ? InterceptionResult<DbTransaction>.SuppressWithResult(connection.BeginTransaction(NivelDsql))
            : result;
    }

    public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection,
        TransactionStartingEventData eventData,
        InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(eventData);
        return Ajustavel(eventData, result)
            ? InterceptionResult<DbTransaction>.SuppressWithResult(await connection.BeginTransactionAsync(NivelDsql, cancellationToken))
            : result;
    }

    private static bool Ajustavel(TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result) =>
        !result.HasResult && eventData.IsolationLevel is IsolationLevel.Unspecified or IsolationLevel.ReadCommitted;
}

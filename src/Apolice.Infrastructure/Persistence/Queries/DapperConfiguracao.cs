using System.Data;
using Dapper;

namespace Apolice.Infrastructure.Persistence.Queries;

// O Npgsql entrega timestamptz e date como DateTime; os DTOs usam DateTimeOffset e DateOnly. Os handlers fazem a
// conversão na leitura e garantem UTC/date na escrita. Configuração global do Dapper, aplicada uma vez por processo.
internal static class DapperConfiguracao
{
    private static int _aplicada;

    public static void Garantir()
    {
        if (Interlocked.Exchange(ref _aplicada, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new ManipuladorDateTimeOffset());
        SqlMapper.AddTypeHandler(new ManipuladorDateOnly());
    }

    private sealed class ManipuladorDateTimeOffset : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset instante => instante,
            DateTime data => new DateTimeOffset(DateTime.SpecifyKind(data, DateTimeKind.Utc)),
            _ => throw new DataException($"Valor {value.GetType().Name} não converte para DateTimeOffset."),
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTimeOffset;
            parameter.Value = value.ToUniversalTime();
        }
    }

    private sealed class ManipuladorDateOnly : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => value switch
        {
            DateOnly data => data,
            DateTime data => DateOnly.FromDateTime(data),
            _ => throw new DataException($"Valor {value.GetType().Name} não converte para DateOnly."),
        };

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }
    }
}

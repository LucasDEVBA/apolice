using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apolice.Infrastructure.Persistence.Configurations;

internal static class ChavesSombra
{
    public const string Id = "Id";
}

// Id de linha filha sem identidade no domínio (fator, histórico): gerado no cliente, como os ids do domínio (v7),
// porque o DSQL não tem sequence/identity e default no banco esconderia a geração.
internal sealed class GeradorIdV7 : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
}

namespace Apolice.Infrastructure.Persistence.Registros;

// Só existe no modelo para a migration criar a tabela: o Numerador lê e grava por SQL (upsert atômico), fora do
// ChangeTracker, porque o número precisa ser confirmado antes do SaveChanges do caso de uso.
internal sealed class SequenciaNumerador
{
    public const int TamanhoMaximoChave = 60;

    private SequenciaNumerador()
    {
        Chave = null!;
    }

    public string Chave { get; private set; }

    public long Ultimo { get; private set; }
}

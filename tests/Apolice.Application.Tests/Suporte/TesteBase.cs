namespace Apolice.Application.Tests.Suporte;

// Um ambiente novo por teste (o xUnit cria uma instância da classe por caso): nada vaza entre testes.
public abstract class TesteBase : IDisposable
{
    private bool _descartado;

    internal Ambiente Amb { get; } = new();

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_descartado)
        {
            return;
        }

        if (disposing)
        {
            Amb.Dispose();
        }

        _descartado = true;
    }
}

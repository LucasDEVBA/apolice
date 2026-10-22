namespace Apolice.Application.Tests.Suporte;

// O ActivitySource e o Meter "Apolice" são estáticos e os ouvintes são globais ao processo: testes que os observam
// rodam isolados, senão capturariam medições e spans das outras classes que rodam em paralelo.
[CollectionDefinition(nameof(TelemetriaGlobal), DisableParallelization = true)]
public sealed class TelemetriaGlobal;

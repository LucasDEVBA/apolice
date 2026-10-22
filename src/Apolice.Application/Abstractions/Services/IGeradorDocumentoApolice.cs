using Apolice.Application.Features.Apolices;

namespace Apolice.Application.Abstractions.Services;

public interface IGeradorDocumentoApolice
{
    // Recebe o mesmo DTO do detalhe: o PDF nunca mostra algo diferente do que a tela mostra. Assíncrono para a infra
    // poder buscar recursos (fontes, logotipo) ou tirar a geração da thread da requisição sem mudar o contrato.
    Task<byte[]> GerarAsync(ApoliceDto apolice, CancellationToken cancellationToken);
}

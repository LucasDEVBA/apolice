namespace Apolice.Application.Features.Apolices.GerarDocumento;

public sealed record GerarDocumentoApoliceQuery(Guid ApoliceId);

public sealed record DocumentoApoliceDto(string NomeArquivo, string TipoConteudo, byte[] Conteudo);

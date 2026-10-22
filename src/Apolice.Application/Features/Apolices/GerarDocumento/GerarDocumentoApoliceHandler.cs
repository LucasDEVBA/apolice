using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Apolices.GerarDocumento;

public sealed class GerarDocumentoApoliceHandler(
    IFeatureFlags flags,
    IApoliceQueries consultas,
    IGeradorDocumentoApolice gerador,
    ICurrentUser usuario,
    TimeProvider tempo) : IQueryHandler<GerarDocumentoApoliceQuery, DocumentoApoliceDto>
{
    public const string TipoConteudoPdf = "application/pdf";

    public async Task<Result<DocumentoApoliceDto>> HandleAsync(GerarDocumentoApoliceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var habilitado = await flags.ExigirAsync(FeatureFlags.DocumentoPdf, cancellationToken);
        if (habilitado.IsFailure)
        {
            return habilitado.Error;
        }

        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        // A carteira é conferida também sobre o que voltou: o PDF traz o CPF completo, e um filtro esquecido no SQL
        // não pode entregá-lo a outro corretor.
        var apolice = await consultas.ObterAsync(query.ApoliceId, escopo.Value.CorretorId, Relogio.Hoje(tempo), cancellationToken);
        if (apolice is null || !escopo.Value.Alcanca(apolice.CorretorId))
        {
            return ComumErros.NaoEncontrado(Entidades.Apolice);
        }

        var conteudo = await gerador.GerarAsync(apolice, cancellationToken);
        return new DocumentoApoliceDto($"apolice-{apolice.Numero}.pdf", TipoConteudoPdf, conteudo);
    }
}

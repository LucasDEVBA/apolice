using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Sistema.ObterFeatures;

// Anônimo de propósito: a tela de login precisa saber se mostra os botões de persona demo.
public sealed class ObterFeaturesHandler(IFeatureFlags flags) : IQueryHandler<ObterFeaturesQuery, FeaturesDto>
{
    public async Task<Result<FeaturesDto>> HandleAsync(ObterFeaturesQuery query, CancellationToken cancellationToken) =>
        new FeaturesDto(
            await flags.IsEnabledAsync(FeatureFlags.LoginDemo, cancellationToken),
            await flags.IsEnabledAsync(FeatureFlags.TriagemAutomaticaSinistros, cancellationToken),
            await flags.IsEnabledAsync(FeatureFlags.DocumentoPdf, cancellationToken),
            await flags.IsEnabledAsync(FeatureFlags.EndossoOnline, cancellationToken));
}

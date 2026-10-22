using Apolice.Application.Abstractions.Services;
using Microsoft.FeatureManagement;

namespace Apolice.Infrastructure.Services;

internal sealed class FeatureFlagsAdapter(IVariantFeatureManager features) : IFeatureFlags
{
    public Task<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken) =>
        features.IsEnabledAsync(flag, cancellationToken).AsTask();
}

namespace Apolice.Application.Abstractions.Services;

// Na infra, sobre IVariantFeatureManager.IsEnabledAsync(string, CancellationToken) do Microsoft.FeatureManagement 4.
public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken);
}

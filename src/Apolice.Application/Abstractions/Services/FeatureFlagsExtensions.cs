using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Abstractions.Services;

internal static class FeatureFlagsExtensions
{
    public static async Task<Result> ExigirAsync(this IFeatureFlags flags, string flag, CancellationToken cancellationToken) =>
        await flags.IsEnabledAsync(flag, cancellationToken) ? Result.Success() : ComumErros.FuncionalidadeDesabilitada;
}

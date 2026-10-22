namespace Apolice.Application.Features.Sistema.ObterFeatures;

public sealed record ObterFeaturesQuery;

public sealed record FeaturesDto(bool LoginDemo, bool TriagemAutomaticaSinistros, bool DocumentoPdf, bool EndossoOnline);

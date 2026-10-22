using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices;

// Corpo de POST /apolices/{id}/endossos/simular e /endossos; o id vem da rota.
public sealed record EndossoRequest(TipoEndosso Tipo, string CoberturaCodigo, decimal? Lmi);

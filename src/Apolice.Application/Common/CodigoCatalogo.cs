namespace Apolice.Application.Common;

// Regra única para códigos de produto e de cobertura em todos os fluxos (rota, filtro, cotação, endosso e sinistro):
// o catálogo é maiúsculo (AUTO, CASCO) e o domínio compara de forma ordinal, então "casco" só funciona se
// todos normalizarem igual antes de chegar a ele.
internal static class CodigoCatalogo
{
    public static string? Normalizar(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();
}

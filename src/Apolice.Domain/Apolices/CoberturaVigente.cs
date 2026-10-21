namespace Apolice.Domain.Apolices;

// Retrato do item numa data (depois de desfeitos os endossos posteriores): é o que o sinistro pode indenizar.
public sealed record CoberturaVigente(string Codigo, string Nome, decimal Lmi, bool PermiteSinistro)
{
    internal static CoberturaVigente De(CoberturaApolice cobertura) =>
        new(cobertura.Codigo, cobertura.Nome, cobertura.Lmi, cobertura.PermiteSinistro);
}

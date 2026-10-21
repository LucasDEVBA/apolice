namespace Apolice.Domain.Tarifacao;

public sealed record ItemTarifado(
    string Codigo,
    string Nome,
    decimal Lmi,
    decimal PremioLiquido,
    bool Obrigatoria,
    bool PermiteSinistro);

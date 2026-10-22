using Apolice.Application.Common;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Features.Cotacoes;

// DadosRisco é o próprio record do domínio: o discriminador "produto" (AUTO|RESID|VIDA) e os campos
// já são o contrato JSON do SPEC 7, e duplicá-los num DTO só abriria espaço para divergência.
public sealed record CotacaoRequest(
    string ProdutoCodigo,
    SeguradoRequest Segurado,
    DadosRisco DadosRisco,
    IReadOnlyList<CoberturaRequest> Coberturas);

public sealed record SeguradoRequest(string Nome, string Cpf, string Email, DateOnly DataNascimento)
{
    // SPEC 9: o corpo da requisição pode ir para log (validação, idempotência); CPF e e-mail completos, não.
    public override string ToString() => $"SeguradoRequest {{ Nome = {Nome}, Cpf = {Mascaramento.Cpf(Cpf)}, Email = {Mascaramento.Email(Email)} }}";
}

public sealed record CoberturaRequest(string Codigo, decimal? Lmi);

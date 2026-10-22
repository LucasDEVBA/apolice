using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Comum;

// Detalhe (cotação/apólice) mostra o CPF completo a quem tem acesso; listagens usam Mascaramento.Cpf.
public sealed record SeguradoDto(string Nome, string Cpf, string Email, DateOnly DataNascimento)
{
    public static SeguradoDto De(Segurado segurado)
    {
        ArgumentNullException.ThrowIfNull(segurado);
        return new SeguradoDto(segurado.Nome, segurado.Cpf.Formatado, segurado.Email.Valor, segurado.DataNascimento);
    }

    // SPEC 9: CPF e e-mail completos nunca em log. Cobre também CotacaoDto e ApoliceDto, cujo ToString gerado chama este.
    public override string ToString() => $"SeguradoDto {{ Nome = {Nome}, Cpf = {Mascaramento.Cpf(Cpf)}, Email = {Mascaramento.Email(Email)} }}";
}

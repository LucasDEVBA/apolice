using DomainCpf = Apolice.Domain.Comum.Cpf;

namespace Apolice.Application.Common;

public static class Mascaramento
{
    private const string CpfOculto = "***.***.***-**";
    private const string EmailOculto = "***";

    // Reaproveita a regra do VO (***.456.789-**); valor que nem é CPF válido sai totalmente oculto.
    public static string Cpf(string? cpf)
    {
        var resultado = DomainCpf.Criar(cpf);
        return resultado.IsSuccess ? resultado.Value.Mascarado : CpfOculto;
    }

    // Para logs: mantém a 1ª letra e o domínio, suficiente para correlacionar sem expor o endereço.
    public static string Email(string? email)
    {
        var arroba = email?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        return arroba <= 0 ? EmailOculto : string.Concat(email![..1], EmailOculto, email[arroba..]);
    }
}

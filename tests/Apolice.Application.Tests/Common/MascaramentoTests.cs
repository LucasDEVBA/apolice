using Apolice.Application.Common;

namespace Apolice.Application.Tests.Common;

public sealed class MascaramentoTests
{
    [Theory]
    [InlineData("52998224725", "***.982.247-**")]
    [InlineData("529.982.247-25", "***.982.247-**")]
    [InlineData("11111111111", "***.***.***-**")]
    [InlineData("123", "***.***.***-**")]
    [InlineData(null, "***.***.***-**")]
    public void Deve_mascarar_o_cpf_quando_lista(string? cpf, string esperado)
    {
        Mascaramento.Cpf(cpf).ShouldBe(esperado);
    }

    [Theory]
    [InlineData("maria.silva@exemplo.com", "m***@exemplo.com")]
    [InlineData("@exemplo.com", "***")]
    [InlineData("sem-arroba", "***")]
    [InlineData(null, "***")]
    public void Deve_mascarar_o_email_quando_vai_para_log(string? email, string esperado)
    {
        Mascaramento.Email(email).ShouldBe(esperado);
    }
}

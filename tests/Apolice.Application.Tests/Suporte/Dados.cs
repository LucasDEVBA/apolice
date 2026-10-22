using Apolice.Application.Features.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Tests.Suporte;

// Fábricas de dados de entrada: valores fixos e calculáveis à mão.
internal static class Dados
{
    public const string CpfValido = "529.982.247-25";
    public const string EmailSegurado = "maria.silva@exemplo.com";

    public static SeguradoRequest Segurado(string nome = "Maria Silva", string cpf = CpfValido, DateOnly? nascimento = null) =>
        new(nome, cpf, EmailSegurado, nascimento ?? new DateOnly(1985, 5, 20));

    // Sem agravos: CASCO 100.000 × 3,2% = 3.200 + RCF_DM 100.000 × 0,6% = 600 → líquido 3.800; IOF 280,44.
    public static CotacaoRequest CotacaoAuto(params CoberturaRequest[] extras) => new(
        CodigosProduto.Auto,
        Segurado(),
        new DadosRiscoAuto(100_000m, 2024, "30140-071", 40, false, 0),
        [new(CodigosCobertura.Casco, null), new(CodigosCobertura.RcfDanosMateriais, 100_000m), .. extras]);

    public static CotacaoRequest CotacaoVida() => new(
        CodigosProduto.Vida,
        Segurado(),
        new DadosRiscoVida(false),
        [new(CodigosCobertura.Morte, 100_000m)]);

    public const decimal PremioLiquidoAuto = 3_800m;
    public const decimal IofAuto = 280.44m;
    public const decimal PremioTotalAuto = 4_080.44m;
}

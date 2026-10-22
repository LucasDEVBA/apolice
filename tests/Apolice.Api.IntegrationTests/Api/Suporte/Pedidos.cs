using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Cotacoes;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Api.IntegrationTests.Api.Suporte;

// Corpos de requisição montados com os próprios records da Application: o JSON sai com o contrato real (camelCase,
// discriminador "produto" nos dados de risco).
internal static class Pedidos
{
    public const string CpfMaria = Cenarios.CpfMaria;
    public const decimal ValorVeiculo = 80_000m;

    public static DateOnly Hoje => Relogio.Hoje(TimeProvider.System);

    public static CotacaoRequest CotacaoAuto(string cpf = CpfMaria) =>
        new(
            CodigosProduto.Auto,
            new SeguradoRequest("Maria da Silva", cpf, "maria.silva@exemplo.test", new DateOnly(1985, 4, 12)),
            new DadosRiscoAuto(ValorVeiculo, 2022, "01310100", 24, UsoComercial: true, ClasseBonus: 3),
            [
                new CoberturaRequest(CodigosCobertura.Casco, null),
                new CoberturaRequest(CodigosCobertura.RcfDanosMateriais, 100_000m),
                new CoberturaRequest(CodigosCobertura.RcfDanosCorporais, 50_000m),
            ]);
}

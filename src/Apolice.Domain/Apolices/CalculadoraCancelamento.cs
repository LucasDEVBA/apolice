using Apolice.Domain.Comum;

namespace Apolice.Domain.Apolices;

// Serviço puro: isola a regra de restituição do estado do agregado (e é testável sem montar apólice).
public static class CalculadoraCancelamento
{
    private const decimal Inteiro = 1m;

    public static SimulacaoCancelamento Simular(MotivoCancelamento motivo, Vigencia vigencia, decimal premioLiquidoBase, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(vigencia);

        if (hoje < vigencia.Inicio)
        {
            return new SimulacaoCancelamento(hoje, 0, vigencia.DiasTotais, MetodoCancelamento.Integral, null, premioLiquidoBase, premioLiquidoBase);
        }

        var decorridos = vigencia.DiasDecorridos(hoje);
        if (motivo == MotivoCancelamento.PedidoDoSegurado)
        {
            var retido = TabelaPrazoCurto.PercentualRetido(decorridos);
            var restituicao = Dinheiro.Arredondar(premioLiquidoBase * (Inteiro - retido));
            return new SimulacaoCancelamento(hoje, decorridos, vigencia.DiasTotais, MetodoCancelamento.PrazoCurto, retido, premioLiquidoBase, restituicao);
        }

        var proRata = Dinheiro.Arredondar(premioLiquidoBase * vigencia.DiasRestantes(hoje) / vigencia.DiasTotais);
        return new SimulacaoCancelamento(hoje, decorridos, vigencia.DiasTotais, MetodoCancelamento.ProRataDie, null, premioLiquidoBase, proRata);
    }
}

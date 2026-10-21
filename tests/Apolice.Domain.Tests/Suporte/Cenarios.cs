using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Tests.Suporte;

// Datas e objetos fixos: nenhum teste depende do relógio real.
internal static class Cenarios
{
    public static readonly Guid CorretorId = Guid.Parse("0199a1b2-0000-7000-8000-000000000001");
    public static readonly Guid AnalistaId = Guid.Parse("0199a1b2-0000-7000-8000-000000000002");

    // 10/03/2026 às 10:00 em Brasília.
    public static readonly DateTimeOffset Agora = Em(2026, 3, 10);
    public static readonly DateOnly Hoje = new(2026, 3, 10);

    public const string NumeroApolice = "AUTO-2026-000042";

    public static DateTimeOffset Em(int ano, int mes, int dia, int hora = 10) =>
        new(ano, mes, dia, hora, 0, 0, Relogio.OffsetBrasilia);

    public static Segurado SeguradoPadrao(DateOnly? nascimento = null) =>
        Segurado.Criar("Maria Silva", "529.982.247-25", "maria.silva@exemplo.com", nascimento ?? new DateOnly(1985, 5, 20)).Value;

    // Sem agravos: condutor de 40 anos, CEP de BH (região neutra), veículo de 2 anos, uso particular, bônus 0.
    public static DadosRiscoAuto DadosAutoNeutro(
        decimal valorVeiculo = 100_000m,
        int anoFabricacao = 2024,
        string cep = "30140-071",
        int idadeCondutor = 40,
        bool usoComercial = false,
        int classeBonus = 0) =>
        new(valorVeiculo, anoFabricacao, cep, idadeCondutor, usoComercial, classeBonus);

    public static CoberturaSolicitada[] CoberturasAutoBasicas() =>
        [new(CodigosCobertura.Casco, null), new(CodigosCobertura.RcfDanosMateriais, 100_000m)];

    public static CoberturaSolicitada[] CoberturasAutoComAssistencia() =>
        [.. CoberturasAutoBasicas(), new(CodigosCobertura.Assistencia24h, null)];

    public static Cotacao CotacaoAuto(
        DateTimeOffset criadaEm,
        IReadOnlyList<CoberturaSolicitada>? coberturas = null,
        bool aceitar = true)
    {
        var produto = CatalogoPadrao.Auto();
        var segurado = SeguradoPadrao();
        var dados = DadosAutoNeutro();
        var tarifacao = Tarifador.Padrao()
            .Calcular(produto, segurado, dados, coberturas ?? CoberturasAutoBasicas(), Relogio.HojeEm(criadaEm))
            .Value;

        var cotacao = Cotacao.Criar(CorretorId, produto, segurado, dados, tarifacao, criadaEm);
        if (aceitar)
        {
            cotacao.Aceitar(criadaEm).IsSuccess.ShouldBeTrue();
        }

        return cotacao;
    }

    public static ApoliceSeguro ApoliceAuto(
        DateTimeOffset emitidaEm,
        DateOnly? inicio = null,
        int parcelas = 1,
        IReadOnlyList<CoberturaSolicitada>? coberturas = null)
    {
        var cotacao = CotacaoAuto(emitidaEm, coberturas);
        return ApoliceSeguro
            .Emitir(cotacao, NumeroApolice, inicio ?? Relogio.HojeEm(emitidaEm), parcelas, emitidaEm)
            .Value;
    }
}

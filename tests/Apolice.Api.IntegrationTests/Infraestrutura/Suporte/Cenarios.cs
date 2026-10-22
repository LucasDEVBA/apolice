using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Infraestrutura.Suporte;

// Montagem de agregados pelos métodos do domínio, dentro de um escopo (repositórios e UoW do mesmo DbContext).
internal sealed class Cenarios(IServiceProvider servicos)
{
    public const string CpfMaria = "52998224725";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public IUnitOfWork Uow => servicos.GetRequiredService<IUnitOfWork>();

    public static Segurado Segurado(string nome = "Maria da Silva", string cpf = CpfMaria, string email = "maria.silva@exemplo.test") =>
        Domain.Comum.Segurado.Criar(nome, cpf, email, new DateOnly(1985, 4, 12)).Value;

    public async Task<Cotacao> CotacaoAutoAsync(Guid corretorId, DateTimeOffset agora, Segurado? segurado = null, bool comVidros = false)
    {
        var produto = (await servicos.GetRequiredService<IProdutoRepository>().ObterAsync(CodigosProduto.Auto, Ct))!;
        List<CoberturaSolicitada> coberturas =
        [
            new(CodigosCobertura.Casco, null),
            new(CodigosCobertura.RcfDanosMateriais, 100_000m),
            new(CodigosCobertura.RcfDanosCorporais, 50_000m),
        ];
        if (comVidros)
        {
            coberturas.Add(new CoberturaSolicitada(CodigosCobertura.Vidros, null));
        }

        var dados = new DadosRiscoAuto(80_000m, 2022, "01310100", 24, UsoComercial: true, ClasseBonus: 3);
        var tarifacao = servicos.GetRequiredService<ITarifador>().Calcular(produto, segurado ?? Segurado(), dados, coberturas, Relogio.HojeEm(agora)).Value;
        var cotacao = Cotacao.Criar(corretorId, produto, segurado ?? Segurado(), dados, tarifacao, agora);
        servicos.GetRequiredService<ICotacaoRepository>().Adicionar(cotacao);
        return cotacao;
    }

    public async Task<ApoliceSeguro> EmitirAsync(Cotacao cotacao, DateTimeOffset agora, int diasAteInicio = 0, int parcelas = 4)
    {
        cotacao.Aceitar(agora).IsSuccess.ShouldBeTrue();
        var ano = Relogio.HojeEm(agora).Year;
        var sequencial = await servicos.GetRequiredService<INumerador>().ProximoAsync(ChavesNumerador.Apolice(cotacao.ProdutoCodigo, ano), Ct);
        var apolice = ApoliceSeguro.Emitir(cotacao, ApoliceSeguro.FormatarNumero(cotacao.ProdutoCodigo, ano, sequencial), Relogio.HojeEm(agora).AddDays(diasAteInicio), parcelas, agora).Value;
        servicos.GetRequiredService<IApoliceRepository>().Adicionar(apolice);
        return apolice;
    }

    public async Task<ApoliceSeguro> ApoliceAutoAsync(Guid corretorId, DateTimeOffset agora, Segurado? segurado = null, int diasAteInicio = 0)
    {
        var cotacao = await CotacaoAutoAsync(corretorId, agora, segurado);
        return await EmitirAsync(cotacao, agora, diasAteInicio);
    }

    public async Task<Sinistro> AbrirSinistroAsync(ApoliceSeguro apolice, Guid usuarioId, DateTimeOffset agora, decimal valor = 6_500m)
    {
        var ano = Relogio.HojeEm(agora).Year;
        var sequencial = await servicos.GetRequiredService<INumerador>().ProximoAsync(ChavesNumerador.Sinistro(ano), Ct);
        var sinistro = Sinistro.Abrir(
            apolice,
            Sinistro.FormatarNumero(ano, sequencial),
            CodigosCobertura.Casco,
            Relogio.HojeEm(agora),
            "Colisão lateral no estacionamento do shopping com amassado na porta.",
            valor,
            usuarioId,
            agora).Value;
        servicos.GetRequiredService<ISinistroRepository>().Adicionar(sinistro);
        return sinistro;
    }

    public async Task<Produto> ProdutoAsync(string codigo) =>
        (await servicos.GetRequiredService<IProdutoRepository>().ObterAsync(codigo, Ct))!;
}

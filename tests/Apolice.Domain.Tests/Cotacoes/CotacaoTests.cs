using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Cotacoes;

// Cotação AUTO base criada em 10/03/2026 10:00 (−03:00): válida até 09/04/2026 10:00.
public sealed class CotacaoTests
{
    private static readonly DateTimeOffset ValidaAte = Cenarios.Agora.AddDays(30);
    private static readonly Guid ApoliceId = Guid.Parse("0199a1b2-0000-7000-8000-0000000000cc");

    [Fact]
    public void Deve_nascer_calculada_com_snapshot_da_tarifacao_quando_criada()
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: false);

        cotacao.Id.Version.ShouldBe(7);
        cotacao.CorretorId.ShouldBe(Cenarios.CorretorId);
        cotacao.ProdutoCodigo.ShouldBe("AUTO");
        cotacao.Segurado.ShouldBe(Cenarios.SeguradoPadrao());
        cotacao.DadosRisco.ShouldBe(Cenarios.DadosAutoNeutro());
        cotacao.Itens.ShouldBe(
        [
            new ItemCotacao("CASCO", "Colisão, incêndio e roubo", 100_000m, 3_200.00m, true, true),
            new ItemCotacao("RCF_DM", "RC danos materiais a terceiros", 100_000m, 600.00m, true, true),
        ]);
        cotacao.Fatores.ShouldBeEmpty();
        (cotacao.PremioLiquido, cotacao.AliquotaIof, cotacao.Iof, cotacao.PremioTotal, cotacao.MaxParcelas)
            .ShouldBe((3_800.00m, 0.0738m, 280.44m, 4_080.44m, 10));
        cotacao.Status.ShouldBe(StatusCotacao.Calculada);
        cotacao.CriadaEm.ShouldBe(Cenarios.Agora);
        cotacao.ValidaAte.ShouldBe(Cenarios.Em(2026, 4, 9)); // criação + 30 dias
        cotacao.ApoliceId.ShouldBeNull();
    }

    [Fact]
    public void Deve_aceitar_quando_calculada_e_ainda_no_ultimo_instante_de_validade()
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: false);

        cotacao.Aceitar(ValidaAte).DeveTerSucesso(); // agora == ValidaAte ainda vale (expira só quando agora > ValidaAte)

        cotacao.Status.ShouldBe(StatusCotacao.Aceita);
    }

    [Fact]
    public void Deve_negar_aceite_quando_expirada()
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: false);

        cotacao.Aceitar(ValidaAte.AddTicks(1)).DeveFalharCom("Cotacao.Expirada", ErrorType.RegraDeNegocio);
        cotacao.Status.ShouldBe(StatusCotacao.Calculada);
    }

    [Fact]
    public void Deve_negar_aceite_quando_ja_aceita()
    {
        Cenarios.CotacaoAuto(Cenarios.Agora).Aceitar(Cenarios.Agora).DeveFalharCom("Cotacao.StatusInvalido", ErrorType.RegraDeNegocio);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deve_recusar_quando_calculada_ou_aceita(bool aceitar)
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: aceitar);

        cotacao.Recusar(Cenarios.Agora).DeveTerSucesso();

        cotacao.Status.ShouldBe(StatusCotacao.Recusada);
        cotacao.StatusEfetivo(Cenarios.Agora).ShouldBe(StatusEfetivoCotacao.Recusada);
    }

    [Fact]
    public void Deve_permitir_recusar_quando_expirada()
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: false);

        cotacao.Recusar(ValidaAte.AddDays(5)).DeveTerSucesso();
    }

    [Fact]
    public void Deve_negar_recusa_quando_ja_recusada_ou_emitida()
    {
        var recusada = Cenarios.CotacaoAuto(Cenarios.Agora);
        recusada.Recusar(Cenarios.Agora);
        var emitida = Cenarios.CotacaoAuto(Cenarios.Agora);
        emitida.MarcarEmitida(ApoliceId, Cenarios.Agora);

        recusada.Recusar(Cenarios.Agora).DeveFalharCom("Cotacao.StatusInvalido");
        emitida.Recusar(Cenarios.Agora).DeveFalharCom("Cotacao.StatusInvalido");
    }

    [Fact]
    public void Deve_marcar_emitida_e_guardar_a_apolice_quando_aceita_e_valida()
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora);

        cotacao.MarcarEmitida(ApoliceId, Cenarios.Agora).DeveTerSucesso();

        cotacao.Status.ShouldBe(StatusCotacao.Emitida);
        cotacao.ApoliceId.ShouldBe(ApoliceId);
        cotacao.StatusEfetivo(ValidaAte.AddDays(1)).ShouldBe(StatusEfetivoCotacao.Emitida); // emitida não "expira"
    }

    [Fact]
    public void Deve_negar_emissao_quando_nao_aceita_ou_expirada()
    {
        var calculada = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: false);
        var aceita = Cenarios.CotacaoAuto(Cenarios.Agora);

        calculada.MarcarEmitida(ApoliceId, Cenarios.Agora).DeveFalharCom("Cotacao.StatusInvalido");
        aceita.MarcarEmitida(ApoliceId, ValidaAte.AddTicks(1)).DeveFalharCom("Cotacao.Expirada");
        aceita.ApoliceId.ShouldBeNull();
        Should.Throw<ArgumentException>(() => aceita.MarcarEmitida(Guid.Empty, Cenarios.Agora));
    }

    [Theory]
    [InlineData(false, 0, StatusEfetivoCotacao.Calculada)]
    [InlineData(true, 0, StatusEfetivoCotacao.Aceita)]
    [InlineData(false, 1, StatusEfetivoCotacao.Expirada)]
    [InlineData(true, 1, StatusEfetivoCotacao.Expirada)]
    public void Deve_derivar_expirada_quando_validade_vencida_e_status_aberto(bool aceitar, int ticksAposValidade, StatusEfetivoCotacao esperado)
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: aceitar);

        cotacao.StatusEfetivo(ValidaAte.AddTicks(ticksAposValidade)).ShouldBe(esperado);
        cotacao.EstaExpirada(ValidaAte.AddTicks(ticksAposValidade)).ShouldBe(ticksAposValidade > 0);
    }

    [Fact]
    public void Deve_lancar_quando_criada_com_dados_incoerentes()
    {
        var produto = CatalogoPadrao.Auto();
        var segurado = Cenarios.SeguradoPadrao();
        var resultado = Tarifador.Padrao().Calcular(produto, segurado, Cenarios.DadosAutoNeutro(), Cenarios.CoberturasAutoBasicas(), Cenarios.Hoje).Value;

        Should.Throw<ArgumentException>(() => Cotacao.Criar(Guid.Empty, produto, segurado, Cenarios.DadosAutoNeutro(), resultado, Cenarios.Agora));
        Should.Throw<ArgumentException>(() => Cotacao.Criar(Cenarios.CorretorId, produto, segurado, new DadosRiscoVida(false), resultado, Cenarios.Agora));
        Should.Throw<ArgumentException>(() =>
            Cotacao.Criar(Cenarios.CorretorId, produto, segurado, Cenarios.DadosAutoNeutro(), ResultadoTarifacao.Criar([], [], 0.0738m), Cenarios.Agora));
        Should.Throw<ArgumentNullException>(() => Cotacao.Criar(Cenarios.CorretorId, null!, segurado, Cenarios.DadosAutoNeutro(), resultado, Cenarios.Agora));
    }

    public static TheoryData<string> TarifacoesForjadas => new() { "item_repetido", "cobertura_fora_do_produto", "aliquota_divergente" };

    [Theory]
    [MemberData(nameof(TarifacoesForjadas))]
    public void Deve_lancar_quando_resultado_nao_saiu_do_tarifador_deste_produto(string caso)
    {
        // ResultadoTarifacao.Criar é público: um fake mal montado não pode virar cotação (e depois PK duplicada na apólice).
        var casco = new ItemTarifado("CASCO", "Colisão, incêndio e roubo", 100_000m, 3_200.00m, true, true);
        var resultado = caso switch
        {
            "item_repetido" => ResultadoTarifacao.Criar([casco, casco], [], 0.0738m),
            "cobertura_fora_do_produto" => ResultadoTarifacao.Criar([casco, new ItemTarifado("MORTE", "Morte", 100_000m, 80m, true, true)], [], 0.0738m),
            _ => ResultadoTarifacao.Criar([casco], [], 0.99m),
        };

        Should.Throw<ArgumentException>(() =>
            Cotacao.Criar(Cenarios.CorretorId, CatalogoPadrao.Auto(), Cenarios.SeguradoPadrao(), Cenarios.DadosAutoNeutro(), resultado, Cenarios.Agora));
    }

    [Fact]
    public void Deve_lancar_quando_status_persistido_desconhecido()
    {
        // Simula a linha corrompida que o EF materializaria sem passar pelas transições.
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora);
        typeof(Cotacao).GetProperty(nameof(Cotacao.Status))!.SetValue(cotacao, (StatusCotacao)99);

        Should.Throw<DadoPersistidoInvalidoException>(() => cotacao.StatusEfetivo(Cenarios.Agora));
    }
}

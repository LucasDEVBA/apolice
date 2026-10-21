using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Apolices;

// Cotação AUTO base (líquido 3.800,00 · IOF 280,44 · total 4.080,44 · até 10x) aceita em 10/03/2026 10:00.
public sealed class EmissaoApoliceTests
{
    [Fact]
    public void Deve_copiar_a_cotacao_e_marca_la_emitida_quando_emite()
    {
        var cotacao = Cenarios.CotacaoAuto(Cenarios.Agora);

        var apolice = ApoliceSeguro.Emitir(cotacao, Cenarios.NumeroApolice, Cenarios.Hoje, 3, Cenarios.Agora).DeveTerSucesso();

        apolice.Id.Version.ShouldBe(7);
        apolice.Numero.ShouldBe("AUTO-2026-000042");
        apolice.CotacaoId.ShouldBe(cotacao.Id);
        apolice.CorretorId.ShouldBe(Cenarios.CorretorId);
        apolice.ProdutoCodigo.ShouldBe("AUTO");
        apolice.Segurado.ShouldBe(cotacao.Segurado);
        apolice.DadosRisco.ShouldBe(cotacao.DadosRisco);
        apolice.Vigencia.ShouldBe(Vigencia.Reconstituir(new DateOnly(2026, 3, 10), new DateOnly(2027, 3, 10))); // início + 1 ano
        apolice.Coberturas.Select(c => (c.Codigo, c.Nome, c.Lmi, c.PremioLiquido, c.Obrigatoria, c.PermiteSinistro)).ShouldBe(
        [
            ("CASCO", "Colisão, incêndio e roubo", 100_000m, 3_200.00m, true, true),
            ("RCF_DM", "RC danos materiais a terceiros", 100_000m, 600.00m, true, true),
        ]);
        (apolice.PremioLiquido, apolice.Iof, apolice.PremioTotal, apolice.AliquotaIof).ShouldBe((3_800.00m, 280.44m, 4_080.44m, 0.0738m));
        apolice.Parcelas.ShouldBe(3);
        apolice.Status.ShouldBe(StatusApolice.Ativa);
        apolice.EmitidaEm.ShouldBe(Cenarios.Agora);
        apolice.DataEmissao.ShouldBe(Cenarios.Hoje);
        apolice.CanceladaEm.ShouldBeNull();
        apolice.DataCancelamento.ShouldBeNull();
        apolice.MotivoCancelamento.ShouldBeNull();
        apolice.ValorRestituicao.ShouldBeNull();
        apolice.BoasVindasEnviadasEm.ShouldBeNull();
        apolice.Endossos.ShouldBeEmpty();
        apolice.AjustesEndosso.ShouldBe(0m);
        apolice.PremioLiquidoVigente.ShouldBe(3_800.00m);

        cotacao.Status.ShouldBe(StatusCotacao.Emitida);
        cotacao.ApoliceId.ShouldBe(apolice.Id);
    }

    [Fact]
    public void Deve_levantar_apolice_emitida_quando_emite()
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);

        var evento = apolice.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ApoliceEmitida>();
        evento.ApoliceId.ShouldBe(apolice.Id);
        evento.Numero.ShouldBe("AUTO-2026-000042");
        evento.CorretorId.ShouldBe(Cenarios.CorretorId);
        evento.ProdutoCodigo.ShouldBe("AUTO");
        evento.SeguradoNome.ShouldBe("Maria Silva");
        evento.SeguradoEmail.ShouldBe("maria.silva@exemplo.com");
        evento.PremioTotal.ShouldBe(4_080.44m);
        evento.VigenciaInicio.ShouldBe(new DateOnly(2026, 3, 10));
        evento.VigenciaFim.ShouldBe(new DateOnly(2027, 3, 10));
        evento.OcorridoEm.ShouldBe(Cenarios.Agora);
    }

    [Theory]
    [InlineData(0, true)] // hoje
    [InlineData(30, true)] // hoje + 30
    [InlineData(-1, false)]
    [InlineData(31, false)]
    public void Deve_validar_quando_inicio_entre_hoje_e_hoje_mais_30(int diasAposHoje, bool valido)
    {
        var resultado = ApoliceSeguro.Emitir(Cenarios.CotacaoAuto(Cenarios.Agora), Cenarios.NumeroApolice, Cenarios.Hoje.AddDays(diasAposHoje), 1, Cenarios.Agora);

        if (valido)
        {
            resultado.DeveTerSucesso();
            return;
        }

        resultado.DeveFalharCom("Apolice.InicioVigenciaInvalido", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_usar_o_dia_de_brasilia_quando_utc_ja_virou()
    {
        // 01:30Z do dia 11 = 22:30 do dia 10 em Brasília: "hoje" ainda é 10/03, então início 10/03 é aceito.
        var agora = new DateTimeOffset(2026, 3, 11, 1, 30, 0, TimeSpan.Zero);

        ApoliceSeguro.Emitir(Cenarios.CotacaoAuto(agora), Cenarios.NumeroApolice, new DateOnly(2026, 3, 10), 1, agora).DeveTerSucesso();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)] // MaxParcelas da cotação
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void Deve_validar_quando_parcelas_entre_1_e_o_maximo_da_cotacao(int parcelas, bool valido)
    {
        var resultado = ApoliceSeguro.Emitir(Cenarios.CotacaoAuto(Cenarios.Agora), Cenarios.NumeroApolice, Cenarios.Hoje, parcelas, Cenarios.Agora);

        resultado.IsSuccess.ShouldBe(valido);
        if (!valido)
        {
            resultado.DeveFalharCom("Apolice.ParcelasInvalidas");
        }
    }

    [Theory]
    [InlineData(5, true)] // MaxParcelas = floor(351,78 / 60) = 5, abaixo do teto de 10
    [InlineData(6, false)]
    public void Deve_respeitar_o_maximo_da_cotacao_quando_ele_fica_abaixo_de_10(int parcelas, bool valido)
    {
        // RESID apartamento de veraneio: INCENDIO 300.000 + ROUBO 10.000 → total 351,78.
        var produto = CatalogoPadrao.Residencial();
        var dados = new DadosRiscoResidencial(300_000m, TipoImovel.Apartamento, "88000-000", Habitual: false);
        var tarifacao = Tarifador.Padrao().Calcular(produto, Cenarios.SeguradoPadrao(), dados, [new("INCENDIO", 300_000m), new("ROUBO", 10_000m)], Cenarios.Hoje).Value;
        var cotacao = Cotacao.Criar(Cenarios.CorretorId, produto, Cenarios.SeguradoPadrao(), dados, tarifacao, Cenarios.Agora);
        cotacao.Aceitar(Cenarios.Agora);

        var resultado = ApoliceSeguro.Emitir(cotacao, "RESID-2026-000001", Cenarios.Hoje, parcelas, Cenarios.Agora);

        cotacao.MaxParcelas.ShouldBe(5);
        if (valido)
        {
            resultado.DeveTerSucesso().Parcelas.ShouldBe(5);
            return;
        }

        resultado.DeveFalharCom("Apolice.ParcelasInvalidas", ErrorType.Validacao);
        resultado.Error.Mensagem.ShouldBe("O número de parcelas deve estar entre 1 e 5.");
    }

    [Fact]
    public void Deve_limitar_a_1_parcela_quando_premio_baixo()
    {
        // RESID só incêndio 150.000: total 72,48 → MaxParcelas = floor(72,48 / 60) = 1.
        var produto = CatalogoPadrao.Residencial();
        var dados = new DadosRiscoResidencial(200_000m, TipoImovel.Casa, "40000-000", true);
        var tarifacao = Tarifador.Padrao().Calcular(produto, Cenarios.SeguradoPadrao(), dados, [new("INCENDIO", 150_000m)], Cenarios.Hoje).Value;
        var cotacao = Cotacao.Criar(Cenarios.CorretorId, produto, Cenarios.SeguradoPadrao(), dados, tarifacao, Cenarios.Agora);
        cotacao.Aceitar(Cenarios.Agora);

        ApoliceSeguro.Emitir(cotacao, "RESID-2026-000001", Cenarios.Hoje, 2, Cenarios.Agora).DeveFalharCom("Apolice.ParcelasInvalidas");
    }

    [Fact]
    public void Deve_negar_quando_cotacao_nao_aceita_expirada_ou_ja_emitida()
    {
        var calculada = Cenarios.CotacaoAuto(Cenarios.Agora, aceitar: false);
        var aceita = Cenarios.CotacaoAuto(Cenarios.Agora);
        var emitida = Cenarios.CotacaoAuto(Cenarios.Agora);
        ApoliceSeguro.Emitir(emitida, Cenarios.NumeroApolice, Cenarios.Hoje, 1, Cenarios.Agora);
        var depoisDaValidade = Cenarios.Agora.AddDays(30).AddTicks(1);

        ApoliceSeguro.Emitir(calculada, Cenarios.NumeroApolice, Cenarios.Hoje, 1, Cenarios.Agora).DeveFalharCom("Cotacao.StatusInvalido");
        ApoliceSeguro.Emitir(aceita, Cenarios.NumeroApolice, Relogio.HojeEm(depoisDaValidade), 1, depoisDaValidade).DeveFalharCom("Cotacao.Expirada");
        ApoliceSeguro.Emitir(emitida, "AUTO-2026-000043", Cenarios.Hoje, 1, Cenarios.Agora).DeveFalharCom("Cotacao.StatusInvalido");
        aceita.Status.ShouldBe(StatusCotacao.Aceita);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AUTO-2026-0000000000000000000042")] // 32 caracteres
    [InlineData("RESID-2026-000042")] // prefixo de outro produto
    [InlineData("AUTO-26-000042")] // ano com 2 dígitos
    [InlineData("AUTO-2026-42")] // sequencial sem os 6 dígitos
    [InlineData("AUTO-2026-00004X")]
    [InlineData("AUTO2026000042")]
    public void Deve_rejeitar_quando_numero_invalido(string numero)
    {
        ApoliceSeguro.Emitir(Cenarios.CotacaoAuto(Cenarios.Agora), numero, Cenarios.Hoje, 1, Cenarios.Agora).DeveFalharCom("Apolice.NumeroInvalido");
    }

    [Fact]
    public void Deve_formatar_com_6_digitos_quando_monta_o_numero()
    {
        ApoliceSeguro.FormatarNumero("AUTO", 2026, 42).ShouldBe("AUTO-2026-000042");
        ApoliceSeguro.FormatarNumero("VIDA", 2026, 1_234_567).ShouldBe("VIDA-2026-1234567"); // passa de 6 dígitos sem cortar
    }

    [Fact]
    public void Deve_aceitar_e_aparar_quando_numero_canonico_com_espacos_ou_sequencial_longo()
    {
        ApoliceSeguro.Emitir(Cenarios.CotacaoAuto(Cenarios.Agora), "  AUTO-2026-000042 ", Cenarios.Hoje, 1, Cenarios.Agora)
            .DeveTerSucesso().Numero.ShouldBe("AUTO-2026-000042");
        ApoliceSeguro.Emitir(Cenarios.CotacaoAuto(Cenarios.Agora), "AUTO-2026-1234567", Cenarios.Hoje, 1, Cenarios.Agora).DeveTerSucesso();
    }

    [Theory]
    [InlineData(-1, StatusEfetivoApolice.Programada)]
    [InlineData(0, StatusEfetivoApolice.Ativa)] // início
    [InlineData(365, StatusEfetivoApolice.Ativa)] // último dia de vigência
    [InlineData(366, StatusEfetivoApolice.Vencida)]
    public void Deve_derivar_status_efetivo_quando_consulta_em_uma_data(int diasAposInicio, StatusEfetivoApolice esperado)
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);

        apolice.StatusEfetivo(Cenarios.Hoje.AddDays(diasAposInicio)).ShouldBe(esperado);
    }

    [Fact]
    public void Deve_marcar_boas_vindas_uma_unica_vez_quando_chamado_de_novo()
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);

        apolice.MarcarBoasVindasEnviadas(Cenarios.Agora.AddMinutes(1));
        apolice.MarcarBoasVindasEnviadas(Cenarios.Agora.AddMinutes(5));

        apolice.BoasVindasEnviadasEm.ShouldBe(Cenarios.Agora.AddMinutes(1));
    }
}

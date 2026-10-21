using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Sinistros;

// Apólice AUTO (CASCO 100.000, RCF_DM 100.000, ASSISTENCIA_24H) com vigência 10/03/2026 → 10/03/2027.
public sealed class SinistroTests
{
    private const string Numero = "SIN-2026-000007";
    private const string Descricao = "Colisão traseira em semáforo na Av. Paulista.";
    private static readonly DateTimeOffset AbertoEm = Cenarios.Em(2026, 5, 5);
    private static readonly DateOnly Ocorrencia = new(2026, 5, 1);
    private static readonly Guid CorretorUsuarioId = Cenarios.CorretorId;

    [Fact]
    public void Deve_copiar_dados_da_apolice_e_levantar_evento_quando_abre()
    {
        var apolice = NovaApolice();

        var sinistro = Abrir(apolice).DeveTerSucesso();

        sinistro.Id.Version.ShouldBe(7);
        (sinistro.Numero, sinistro.ApoliceId, sinistro.ApoliceNumero).ShouldBe((Numero, apolice.Id, Cenarios.NumeroApolice));
        (sinistro.CorretorId, sinistro.ProdutoCodigo, sinistro.SeguradoNome).ShouldBe((Cenarios.CorretorId, "AUTO", "Maria Silva"));
        sinistro.SeguradoEmail.ShouldBe(Email.Criar("maria.silva@exemplo.com").Value);
        (sinistro.CoberturaCodigo, sinistro.CoberturaNome, sinistro.LmiCobertura).ShouldBe(("CASCO", "Colisão, incêndio e roubo", 100_000m));
        (sinistro.DataOcorrencia, sinistro.Descricao, sinistro.ValorReclamado).ShouldBe((Ocorrencia, Descricao, 15_000m));
        sinistro.ValorIndenizacao.ShouldBeNull();
        sinistro.MotivoNegativa.ShouldBeNull();
        sinistro.Triagem.ShouldBeNull();
        sinistro.Status.ShouldBe(StatusSinistro.Aberto);
        sinistro.AbertoEm.ShouldBe(AbertoEm);
        sinistro.Historico.ShouldBe([new HistoricoSinistro(StatusSinistro.Aberto, null, CorretorUsuarioId, AbertoEm)]);

        var evento = sinistro.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<SinistroAberto>();
        (evento.SinistroId, evento.Numero, evento.ApoliceId).ShouldBe((sinistro.Id, Numero, apolice.Id));
        (evento.CoberturaCodigo, evento.ValorReclamado, evento.SeguradoEmail, evento.OcorridoEm)
            .ShouldBe(("CASCO", 15_000m, "maria.silva@exemplo.com", AbertoEm));
    }

    [Fact]
    public void Deve_normalizar_descricao_e_valor_quando_abre()
    {
        var sinistro = Abrir(NovaApolice(), descricao: $"  {Descricao}  ", valor: 15_000.005m).DeveTerSucesso();

        sinistro.Descricao.ShouldBe(Descricao);
        sinistro.ValorReclamado.ShouldBe(15_000.01m); // arredondado a centavos (AwayFromZero)
    }

    [Fact]
    public void Deve_aceitar_quando_ocorrencia_no_proprio_dia_da_abertura()
    {
        Abrir(NovaApolice(), ocorrencia: new DateOnly(2026, 5, 5)).DeveTerSucesso();
    }

    [Fact]
    public void Deve_rejeitar_quando_ocorrencia_futura()
    {
        Abrir(NovaApolice(), ocorrencia: new DateOnly(2026, 5, 6)).DeveFalharCom("Sinistro.OcorrenciaFutura", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_quando_ocorrencia_e_hoje_em_utc_mas_amanha_em_brasilia()
    {
        // 01:30Z do dia 05 = 22:30 do dia 04 em Brasília: uma ocorrência em 05/05 ainda é futura.
        var agora = new DateTimeOffset(2026, 5, 5, 1, 30, 0, TimeSpan.Zero);

        Abrir(NovaApolice(), ocorrencia: new DateOnly(2026, 5, 5), agora: agora).DeveFalharCom("Sinistro.OcorrenciaFutura");
        Abrir(NovaApolice(), ocorrencia: new DateOnly(2026, 5, 4), agora: agora).DeveTerSucesso();
    }

    [Fact]
    public void Deve_rejeitar_quando_ocorrencia_antes_do_inicio_da_vigencia()
    {
        Abrir(NovaApolice(), ocorrencia: new DateOnly(2026, 3, 9)).DeveFalharCom("Sinistro.ForaDaVigencia", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public void Deve_aceitar_quando_ocorrencia_no_ultimo_dia_de_vigencia()
    {
        var aposOFim = Cenarios.Em(2027, 3, 12);

        Abrir(NovaApolice(), ocorrencia: new DateOnly(2027, 3, 10), agora: aposOFim).DeveTerSucesso();
        Abrir(NovaApolice(), ocorrencia: new DateOnly(2027, 3, 11), agora: aposOFim).DeveFalharCom("Sinistro.ForaDaVigencia");
    }

    [Fact]
    public void Deve_exigir_ocorrencia_anterior_ao_cancelamento_quando_apolice_cancelada()
    {
        var apolice = NovaApolice();
        apolice.Cancelar(MotivoCancelamento.PedidoDoSegurado, Cenarios.Em(2026, 6, 1));
        var agora = Cenarios.Em(2026, 6, 5);

        Abrir(apolice, ocorrencia: new DateOnly(2026, 5, 31), agora: agora).DeveTerSucesso();
        Abrir(apolice, ocorrencia: new DateOnly(2026, 6, 1), agora: agora).DeveFalharCom("Sinistro.AposCancelamento", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public void Deve_usar_o_dia_de_brasilia_do_cancelamento_quando_cancelada_as_22h()
    {
        // 01:00Z de 02/06 = 22:00 de 01/06 em Brasília: a data do cancelamento é 01/06, não 02/06.
        var apolice = NovaApolice();
        apolice.Cancelar(MotivoCancelamento.Inadimplencia, new DateTimeOffset(2026, 6, 2, 1, 0, 0, TimeSpan.Zero));
        var agora = Cenarios.Em(2026, 6, 5);

        apolice.DataCancelamento.ShouldBe(new DateOnly(2026, 6, 1));
        Abrir(apolice, ocorrencia: new DateOnly(2026, 6, 1), agora: agora).DeveFalharCom("Sinistro.AposCancelamento");
        Abrir(apolice, ocorrencia: new DateOnly(2026, 5, 31), agora: agora).DeveTerSucesso();
    }

    [Fact]
    public void Deve_rejeitar_quando_apolice_cancelada_antes_mesmo_do_inicio()
    {
        // Emitida em 10/03 com início em 20/03 e cancelada em 12/03: nenhum dia de vigência ficou coberto.
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora, new DateOnly(2026, 3, 20));
        apolice.Cancelar(MotivoCancelamento.PedidoDoSegurado, Cenarios.Em(2026, 3, 12));

        Abrir(apolice, ocorrencia: new DateOnly(2026, 3, 25), agora: Cenarios.Em(2026, 3, 26))
            .DeveFalharCom("Sinistro.AposCancelamento", ErrorType.RegraDeNegocio);
    }

    [Fact]
    public void Deve_rejeitar_quando_cobertura_ausente_ou_sem_indenizacao()
    {
        Abrir(NovaApolice(), cobertura: "VIDROS").DeveFalharCom("Sinistro.CoberturaNaoContratada", ErrorType.RegraDeNegocio);
        Abrir(NovaApolice(), cobertura: "ASSISTENCIA_24H").DeveFalharCom("Sinistro.CoberturaSemIndenizacao", ErrorType.RegraDeNegocio);
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Deve_validar_quando_descricao_nas_bordas_de_20_a_2000(int tamanho, bool valido)
    {
        var resultado = Abrir(NovaApolice(), descricao: new string('d', tamanho));

        resultado.IsSuccess.ShouldBe(valido);
        if (!valido)
        {
            resultado.DeveFalharCom("Sinistro.DescricaoInvalida", ErrorType.Validacao);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.004")] // arredonda para 0,00
    public void Deve_rejeitar_quando_valor_reclamado_nao_positivo(string valor)
    {
        Abrir(NovaApolice(), valor: Valor.De(valor)).DeveFalharCom("Sinistro.ValorReclamadoInvalido");
    }

    [Fact]
    public void Deve_rejeitar_quando_numero_ou_descricao_ausentes()
    {
        Abrir(NovaApolice(), numero: " ").DeveFalharCom("Sinistro.NumeroInvalido");
        Abrir(NovaApolice(), descricao: null!).DeveFalharCom("Sinistro.DescricaoInvalida");
    }

    [Theory]
    [InlineData("AUTO-2026-000007")] // prefixo de apólice
    [InlineData("SIN-26-000007")] // ano com 2 dígitos
    [InlineData("SIN-2026-7")] // sequencial sem os 6 dígitos
    [InlineData("SIN-2026-00000A")]
    [InlineData("SIN-2026-000007-1")]
    public void Deve_rejeitar_quando_numero_fora_do_formato_canonico(string numero)
    {
        Abrir(NovaApolice(), numero: numero).DeveFalharCom("Sinistro.NumeroInvalido", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_aparar_o_numero_quando_vem_com_espacos()
    {
        Abrir(NovaApolice(), numero: "  SIN-2026-000007 ").DeveTerSucesso().Numero.ShouldBe(Numero);
    }

    [Fact]
    public void Deve_gravar_historico_e_eventos_quando_aberto_analisado_aprovado_e_pago()
    {
        var sinistro = Abrir(NovaApolice()).Value;
        sinistro.ClearDomainEvents();
        var (t1, t2, t3) = (AbertoEm.AddHours(1), AbertoEm.AddHours(2), AbertoEm.AddDays(3));

        sinistro.IniciarAnalise(Cenarios.AnalistaId, t1).DeveTerSucesso();
        sinistro.Aprovar(12_000m, "  Orçamento conferido.  ", Cenarios.AnalistaId, t2).DeveTerSucesso();
        sinistro.RegistrarPagamento(Cenarios.AnalistaId, t3).DeveTerSucesso();

        sinistro.Status.ShouldBe(StatusSinistro.Pago);
        sinistro.ValorIndenizacao.ShouldBe(12_000m);
        sinistro.Historico.ShouldBe(
        [
            new HistoricoSinistro(StatusSinistro.Aberto, null, CorretorUsuarioId, AbertoEm),
            new HistoricoSinistro(StatusSinistro.EmAnalise, null, Cenarios.AnalistaId, t1),
            new HistoricoSinistro(StatusSinistro.Aprovado, "Orçamento conferido.", Cenarios.AnalistaId, t2),
            new HistoricoSinistro(StatusSinistro.Pago, null, Cenarios.AnalistaId, t3),
        ]);
        sinistro.Historico.Select(h => (h.Status, h.UsuarioId)).ShouldBe(
        [
            (StatusSinistro.Aberto, (Guid?)CorretorUsuarioId), (StatusSinistro.EmAnalise, Cenarios.AnalistaId),
            (StatusSinistro.Aprovado, Cenarios.AnalistaId), (StatusSinistro.Pago, Cenarios.AnalistaId),
        ]);
        var eventos = sinistro.DomainEvents.ToList();
        eventos.Count.ShouldBe(2);
        var aprovado = eventos[0].ShouldBeOfType<SinistroAprovado>();
        (aprovado.SinistroId, aprovado.Numero, aprovado.ValorIndenizacao, aprovado.SeguradoEmail, aprovado.OcorridoEm)
            .ShouldBe((sinistro.Id, Numero, 12_000m, "maria.silva@exemplo.com", t2));
        var pago = eventos[1].ShouldBeOfType<SinistroPago>();
        (pago.SinistroId, pago.Numero, pago.SeguradoEmail, pago.OcorridoEm).ShouldBe((sinistro.Id, Numero, "maria.silva@exemplo.com", t3));
    }

    [Fact]
    public void Deve_negar_com_motivo_e_levantar_evento_quando_em_analise()
    {
        var sinistro = EmAnalise();
        sinistro.ClearDomainEvents();
        var agora = AbertoEm.AddDays(1);

        sinistro.Negar("  Dano preexistente à contratação.  ", Cenarios.AnalistaId, agora).DeveTerSucesso();

        sinistro.Status.ShouldBe(StatusSinistro.Negado);
        sinistro.MotivoNegativa.ShouldBe("Dano preexistente à contratação.");
        sinistro.Historico[^1].ShouldBe(new HistoricoSinistro(StatusSinistro.Negado, "Dano preexistente à contratação.", Cenarios.AnalistaId, agora));
        var evento = sinistro.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<SinistroNegado>();
        (evento.SinistroId, evento.Numero, evento.Motivo, evento.SeguradoEmail, evento.OcorridoEm)
            .ShouldBe((sinistro.Id, Numero, "Dano preexistente à contratação.", "maria.silva@exemplo.com", agora));
    }

    [Theory]
    [InlineData("Duplicado", false)] // 9 caracteres
    [InlineData("Duplicado.", true)] // 10 caracteres
    [InlineData("   ", false)]
    public void Deve_validar_quando_motivo_de_negativa_na_borda_de_10(string motivo, bool valido)
    {
        var resultado = EmAnalise().Negar(motivo, Cenarios.AnalistaId, AbertoEm.AddDays(1));

        resultado.IsSuccess.ShouldBe(valido);
        if (!valido)
        {
            resultado.DeveFalharCom("Sinistro.MotivoNegativaInvalido", ErrorType.Validacao);
        }
    }

    [Fact]
    public void Deve_rejeitar_negativa_quando_motivo_nulo()
    {
        EmAnalise().Negar(null!, Cenarios.AnalistaId, AbertoEm.AddDays(1)).DeveFalharCom("Sinistro.MotivoNegativaInvalido");
    }

    [Fact]
    public void Deve_negar_quando_transicao_fora_da_maquina_de_estados()
    {
        var aberto = Abrir(NovaApolice()).Value;
        var emAnalise = EmAnalise();
        var negado = EmAnalise();
        negado.Negar("Dano preexistente à contratação.", Cenarios.AnalistaId, AbertoEm.AddDays(1));
        var agora = AbertoEm.AddDays(2);

        aberto.Aprovar(1_000m, null, Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);
        aberto.Negar("Motivo qualquer de teste.", Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        aberto.RegistrarPagamento(Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        emAnalise.IniciarAnalise(Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        emAnalise.RegistrarPagamento(Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        negado.RegistrarPagamento(Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        negado.Aprovar(1_000m, null, Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        aberto.Historico.Count.ShouldBe(1);
    }

    [Fact]
    public void Deve_negar_quando_aprovado_tenta_voltar_para_analise_ou_ser_decidido_de_novo()
    {
        var aprovado = EmAnalise();
        aprovado.Aprovar(1_000m, null, Cenarios.AnalistaId, AbertoEm.AddDays(1));
        aprovado.ClearDomainEvents();
        var agora = AbertoEm.AddDays(2);

        aprovado.IniciarAnalise(Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        aprovado.Aprovar(500m, null, Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");
        aprovado.Negar("Revisão posterior da análise.", Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.TransicaoInvalida");

        (aprovado.Status, aprovado.ValorIndenizacao).ShouldBe((StatusSinistro.Aprovado, (decimal?)1_000m));
        aprovado.MotivoNegativa.ShouldBeNull();
        aprovado.Historico.Count.ShouldBe(3);
        aprovado.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Deve_negar_segundo_pagamento_sem_historico_nem_evento_quando_ja_pago()
    {
        var pago = EmAnalise();
        pago.Aprovar(1_000m, null, Cenarios.AnalistaId, AbertoEm.AddDays(1));
        pago.RegistrarPagamento(Cenarios.AnalistaId, AbertoEm.AddDays(2));
        pago.ClearDomainEvents();

        pago.RegistrarPagamento(Cenarios.AnalistaId, AbertoEm.AddDays(3)).DeveFalharCom("Sinistro.TransicaoInvalida", ErrorType.RegraDeNegocio);

        pago.Status.ShouldBe(StatusSinistro.Pago);
        pago.Historico.Count.ShouldBe(4);
        pago.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Deve_lancar_quando_usuario_vazio_e_bug_do_chamador()
    {
        var sinistro = EmAnalise();
        var agora = AbertoEm.AddDays(1);

        Should.Throw<ArgumentException>(() => Sinistro.Abrir(NovaApolice(), Numero, "CASCO", Ocorrencia, Descricao, 15_000m, Guid.Empty, AbertoEm));
        Should.Throw<ArgumentException>(() => Abrir(NovaApolice()).Value.IniciarAnalise(Guid.Empty, agora));
        Should.Throw<ArgumentException>(() => sinistro.Aprovar(1_000m, null, Guid.Empty, agora));
        Should.Throw<ArgumentException>(() => sinistro.Negar("Dano preexistente à contratação.", Guid.Empty, agora));
        Should.Throw<ArgumentException>(() => sinistro.RegistrarPagamento(Guid.Empty, agora));
        sinistro.Historico.Count.ShouldBe(2);
    }

    [Fact]
    public void Deve_aceitar_abertura_sem_autor_quando_usuario_nulo()
    {
        var sinistro = Sinistro.Abrir(NovaApolice(), Numero, "CASCO", Ocorrencia, Descricao, 15_000m, null, AbertoEm).DeveTerSucesso();

        sinistro.Historico.ShouldHaveSingleItem().UsuarioId.ShouldBeNull();
    }

    [Theory]
    [InlineData("15000", true)] // = valor reclamado (menor que o LMI)
    [InlineData("15000.01", false)]
    public void Deve_limitar_indenizacao_quando_reclamado_abaixo_do_lmi(string indenizacao, bool valido)
    {
        var resultado = EmAnalise().Aprovar(Valor.De(indenizacao), null, Cenarios.AnalistaId, AbertoEm.AddDays(1));

        resultado.IsSuccess.ShouldBe(valido);
        if (!valido)
        {
            resultado.DeveFalharCom("Sinistro.IndenizacaoAcimaDoLimite", ErrorType.RegraDeNegocio);
        }
    }

    [Theory]
    [InlineData("100000", true)] // = LMI, menor que o reclamado de 120.000
    [InlineData("100000.01", false)]
    public void Deve_limitar_indenizacao_ao_lmi_quando_reclamado_acima_do_lmi(string indenizacao, bool valido)
    {
        var sinistro = EmAnalise(cobertura: "RCF_DM", valor: 120_000m);

        sinistro.Aprovar(Valor.De(indenizacao), null, Cenarios.AnalistaId, AbertoEm.AddDays(1)).IsSuccess.ShouldBe(valido);
    }

    [Fact]
    public void Deve_rejeitar_quando_indenizacao_zero_ou_observacao_longa()
    {
        var sinistro = EmAnalise();
        var agora = AbertoEm.AddDays(1);

        sinistro.Aprovar(0m, null, Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.ValorIndenizacaoInvalido", ErrorType.Validacao);
        sinistro.Aprovar(1_000m, new string('o', 2001), Cenarios.AnalistaId, agora).DeveFalharCom("Sinistro.ObservacaoInvalida");
        sinistro.Status.ShouldBe(StatusSinistro.EmAnalise);
    }

    [Fact]
    public void Deve_gravar_observacao_vazia_como_nula_quando_aprova()
    {
        var sinistro = EmAnalise();

        sinistro.Aprovar(1_000m, "   ", Cenarios.AnalistaId, AbertoEm.AddDays(1)).DeveTerSucesso();

        sinistro.Historico[^1].Observacao.ShouldBeNull();
    }

    [Fact]
    public void Deve_formatar_com_6_digitos_quando_monta_o_numero()
    {
        Sinistro.FormatarNumero(2026, 7).ShouldBe("SIN-2026-000007");
    }

    private static ApoliceSeguro NovaApolice() =>
        Cenarios.ApoliceAuto(Cenarios.Agora, coberturas: Cenarios.CoberturasAutoComAssistencia());

    private static Result<Sinistro> Abrir(
        ApoliceSeguro apolice,
        string numero = Numero,
        string cobertura = "CASCO",
        DateOnly? ocorrencia = null,
        string descricao = Descricao,
        decimal valor = 15_000m,
        DateTimeOffset? agora = null) =>
        Sinistro.Abrir(apolice, numero, cobertura, ocorrencia ?? Ocorrencia, descricao, valor, CorretorUsuarioId, agora ?? AbertoEm);

    private static Sinistro EmAnalise(string cobertura = "CASCO", decimal valor = 15_000m)
    {
        var sinistro = Abrir(NovaApolice(), cobertura: cobertura, valor: valor).Value;
        sinistro.IniciarAnalise(Cenarios.AnalistaId, AbertoEm.AddHours(1));
        return sinistro;
    }
}

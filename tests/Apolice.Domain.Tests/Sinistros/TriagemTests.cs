using Apolice.Domain.Comum;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Sinistros;

// Apólice AUTO com início em 10/03/2026; CASCO com LMI 100.000 (80% = 80.000).
public sealed class TriagemTests
{
    private const string Descricao = "Colisão traseira em semáforo na Av. Paulista.";
    private static readonly DateOnly InicioVigencia = new(2026, 3, 10);

    [Fact]
    public void Deve_atingir_score_maximo_quando_todos_os_alertas_disparam()
    {
        // Ocorrência 20/03 (10 dias após o início, +40), reclamado 120.000 > 80.000 (+30) e > LMI (+20),
        // aberto em 25/04 (36 dias após a ocorrência, +10) → 100.
        var sinistro = Abrir(new DateOnly(2026, 3, 20), 120_000m, Cenarios.Em(2026, 4, 25));

        var triagem = AvaliadorTriagem.Avaliar(sinistro, InicioVigencia, new DateOnly(2026, 4, 25));

        triagem.Score.ShouldBe(TriagemSinistro.ScoreMaximo);
        triagem.Alertas.ShouldBe(["VIGENCIA_RECENTE", "VALOR_PROXIMO_LMI", "VALOR_ACIMA_LMI", "COMUNICACAO_TARDIA"]);
    }

    [Fact]
    public void Deve_zerar_score_quando_nenhum_alerta_dispara()
    {
        // Ocorrência 01/05 (52 dias após o início), 15.000 de 100.000, aberto 4 dias depois.
        var triagem = AvaliadorTriagem.Avaliar(Abrir(new DateOnly(2026, 5, 1), 15_000m, Cenarios.Em(2026, 5, 5)), InicioVigencia, new DateOnly(2026, 5, 5));

        triagem.Score.ShouldBe(0);
        triagem.Alertas.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(30, 40)] // 09/04: exatamente 30 dias após o início ainda é "recente"
    [InlineData(31, 0)]
    public void Deve_pontuar_vigencia_recente_quando_ocorrencia_ate_30_dias_apos_o_inicio(int diasAposInicio, int score)
    {
        var ocorrencia = InicioVigencia.AddDays(diasAposInicio);
        var abertoEm = new DateTimeOffset(ocorrencia.ToDateTime(new TimeOnly(10, 0)), Relogio.OffsetBrasilia);

        AvaliadorTriagem.Avaliar(Abrir(ocorrencia, 15_000m, abertoEm), InicioVigencia, ocorrencia).Score.ShouldBe(score);
    }

    [Theory]
    [InlineData("80000", 0)] // exatamente 80% não dispara (precisa ser > 80%)
    [InlineData("80000.01", 30)]
    [InlineData("100000", 30)] // = LMI: só o alerta de 80%
    [InlineData("100000.01", 50)] // > LMI: 30 + 20
    public void Deve_pontuar_quando_reclamado_passa_de_80_por_cento_ou_do_lmi(string reclamado, int score)
    {
        var sinistro = Abrir(new DateOnly(2026, 5, 1), Valor.De(reclamado), Cenarios.Em(2026, 5, 5));

        AvaliadorTriagem.Avaliar(sinistro, InicioVigencia, new DateOnly(2026, 5, 5)).Score.ShouldBe(score);
    }

    [Theory]
    [InlineData(30, 0)] // aberto em 31/05: 30 dias após a ocorrência de 01/05
    [InlineData(31, 10)] // 01/06: 31 dias → comunicação tardia
    public void Deve_pontuar_comunicacao_tardia_quando_aberto_mais_de_30_dias_apos_a_ocorrencia(int diasAposOcorrencia, int score)
    {
        var abertoEm = Cenarios.Em(2026, 5, 1).AddDays(diasAposOcorrencia);
        var sinistro = Abrir(new DateOnly(2026, 5, 1), 15_000m, abertoEm);

        AvaliadorTriagem.Avaliar(sinistro, InicioVigencia, Relogio.HojeEm(abertoEm)).Score.ShouldBe(score);
    }

    [Fact]
    public void Deve_medir_atraso_no_dia_de_brasilia_quando_abertura_perto_da_meia_noite_utc()
    {
        // Ocorrência 20/03 (+40) e reclamado 90.000 > 80.000 (+30). Aberto às 01:00Z de 20/04 = 22:00 de 19/04
        // em Brasília: 30 dias após a ocorrência, ainda não é comunicação tardia (em UTC seriam 31 → score 80).
        var abertoEm = new DateTimeOffset(2026, 4, 20, 1, 0, 0, TimeSpan.Zero);
        var sinistro = Abrir(new DateOnly(2026, 3, 20), 90_000m, abertoEm);

        var triagem = AvaliadorTriagem.Avaliar(sinistro, InicioVigencia, new DateOnly(2026, 4, 19));

        triagem.Score.ShouldBe(70);
        triagem.Alertas.ShouldBe(["VIGENCIA_RECENTE", "VALOR_PROXIMO_LMI"]);
    }

    [Fact]
    public void Deve_medir_atraso_pela_data_de_abertura_quando_avaliado_dias_depois()
    {
        // Aberto 4 dias após a ocorrência; reavaliar em dezembro não pode virar "comunicação tardia".
        var sinistro = Abrir(new DateOnly(2026, 5, 1), 15_000m, Cenarios.Em(2026, 5, 5));

        AvaliadorTriagem.Avaliar(sinistro, InicioVigencia, new DateOnly(2026, 12, 1)).Alertas.ShouldNotContain("COMUNICACAO_TARDIA");
    }

    [Fact]
    public void Deve_ignorar_segunda_triagem_quando_ja_registrada()
    {
        // Ocorrência 20/03 (10 dias após o início) aberta em 25/03: só VIGENCIA_RECENTE (+40).
        var abertoEm = Cenarios.Em(2026, 3, 25);
        var sinistro = Abrir(new DateOnly(2026, 3, 20), 15_000m, abertoEm);
        var triagem = AvaliadorTriagem.Avaliar(sinistro, InicioVigencia, new DateOnly(2026, 3, 25));
        var registradaEm = abertoEm.AddSeconds(3);

        sinistro.RegistrarTriagem(triagem, registradaEm);
        sinistro.RegistrarTriagem(TriagemSinistro.Criar(100, ["VALOR_ACIMA_LMI"], registradaEm).Value, registradaEm.AddMinutes(1));

        sinistro.Triagem.ShouldNotBeNull();
        sinistro.Triagem.Score.ShouldBe(40);
        sinistro.Triagem.Alertas.ShouldBe(["VIGENCIA_RECENTE"]);
        sinistro.Triagem.TriadoEm.ShouldBe(registradaEm);
        Should.Throw<ArgumentNullException>(() => sinistro.RegistrarTriagem(null!, registradaEm));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Deve_rejeitar_triagem_quando_score_fora_de_0_a_100(int score)
    {
        TriagemSinistro.Criar(score, [], Cenarios.Agora).DeveFalharCom("Sinistro.TriagemInvalida", ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_triagem_quando_sem_lista_ou_com_alerta_vazio()
    {
        TriagemSinistro.Criar(10, null, Cenarios.Agora).DeveFalharCom("Sinistro.TriagemInvalida");
        TriagemSinistro.Criar(10, [" "], Cenarios.Agora).DeveFalharCom("Sinistro.TriagemInvalida");
    }

    [Fact]
    public void Deve_ser_igual_quando_triagens_tem_os_mesmos_alertas()
    {
        var a = TriagemSinistro.Criar(40, ["VIGENCIA_RECENTE"], Cenarios.Agora).Value;
        var b = TriagemSinistro.Criar(40, ["VIGENCIA_RECENTE"], Cenarios.Agora).Value;
        var c = TriagemSinistro.Criar(40, ["VALOR_ACIMA_LMI"], Cenarios.Agora).Value;

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(c);
        a.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Deve_copiar_a_lista_de_alertas_quando_cria()
    {
        var alertas = new List<string> { "VIGENCIA_RECENTE" };
        var triagem = TriagemSinistro.Criar(40, alertas, Cenarios.Agora).Value;

        alertas.Add("VALOR_ACIMA_LMI");

        triagem.Alertas.ShouldBe(["VIGENCIA_RECENTE"]);
    }

    [Fact]
    public void Deve_reconstituir_triagem_valida_e_lancar_quando_corrompida()
    {
        TriagemSinistro.Reconstituir(40, ["VIGENCIA_RECENTE"], Cenarios.Agora).Score.ShouldBe(40);
        Should.Throw<DadoPersistidoInvalidoException>(() => TriagemSinistro.Reconstituir(140, [], Cenarios.Agora));
    }

    [Fact]
    public void Deve_lancar_quando_sinistro_nulo()
    {
        Should.Throw<ArgumentNullException>(() => AvaliadorTriagem.Avaliar(null!, InicioVigencia, InicioVigencia));
    }

    private static Sinistro Abrir(DateOnly ocorrencia, decimal valorReclamado, DateTimeOffset abertoEm)
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora);
        return Sinistro.Abrir(apolice, "SIN-2026-000001", "CASCO", ocorrencia, Descricao, valorReclamado, Cenarios.CorretorId, abertoEm).Value;
    }
}

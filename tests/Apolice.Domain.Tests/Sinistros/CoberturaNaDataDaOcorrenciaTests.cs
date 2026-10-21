using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Tests.Suporte;

namespace Apolice.Domain.Tests.Sinistros;

// O sinistro indeniza o que estava contratado no DIA DA OCORRÊNCIA: endossos com efeito posterior são desfeitos.
// Apólice AUTO emitida em 10/03/2026 (vigência 10/03/2026 → 10/03/2027); endossos em 10/09/2026.
public sealed class CoberturaNaDataDaOcorrenciaTests
{
    private const string Descricao = "Pedra lançada por caminhão trincou o para-brisa.";
    private static readonly DateTimeOffset DiaDoEndosso = Cenarios.Em(2026, 9, 10);
    private static readonly DateTimeOffset DepoisDoEndosso = Cenarios.Em(2026, 9, 15);
    private static readonly DateOnly AntesDoEndosso = new(2026, 4, 1);
    private static readonly Tarifador Tarifador = Tarifador.Padrao();

    [Fact]
    public void Deve_negar_quando_cobertura_foi_incluida_por_endosso_depois_da_ocorrencia()
    {
        var apolice = ApoliceComAssistencia();
        Endossar(apolice, new(TipoEndosso.InclusaoCobertura, "VIDROS"), DiaDoEndosso);

        Abrir(apolice, "VIDROS", AntesDoEndosso).DeveFalharCom("Sinistro.CoberturaNaoContratada", ErrorType.RegraDeNegocio);
        Abrir(apolice, "VIDROS", new DateOnly(2026, 9, 9)).DeveFalharCom("Sinistro.CoberturaNaoContratada");
    }

    [Fact]
    public void Deve_aceitar_quando_ocorrencia_no_proprio_dia_do_efeito_da_inclusao()
    {
        var apolice = ApoliceComAssistencia();
        Endossar(apolice, new(TipoEndosso.InclusaoCobertura, "VIDROS"), DiaDoEndosso);

        var sinistro = Abrir(apolice, "VIDROS", new DateOnly(2026, 9, 10)).DeveTerSucesso();

        (sinistro.CoberturaCodigo, sinistro.CoberturaNome, sinistro.LmiCobertura).ShouldBe(("VIDROS", "Vidros, faróis e retrovisores", 3_000m));
    }

    [Fact]
    public void Deve_usar_o_lmi_da_data_da_ocorrencia_quando_lmi_aumentou_depois()
    {
        var apolice = ApoliceComAssistencia();
        Endossar(apolice, new(TipoEndosso.AlteracaoLmi, "RCF_DM", 500_000m), DiaDoEndosso);

        var anterior = Abrir(apolice, "RCF_DM", AntesDoEndosso, valor: 400_000m).DeveTerSucesso();
        var posterior = Abrir(apolice, "RCF_DM", new DateOnly(2026, 9, 12), valor: 400_000m).DeveTerSucesso();

        anterior.LmiCobertura.ShouldBe(100_000m);
        posterior.LmiCobertura.ShouldBe(500_000m);
        anterior.IniciarAnalise(Cenarios.AnalistaId, DepoisDoEndosso).DeveTerSucesso();
        anterior.Aprovar(400_000m, null, Cenarios.AnalistaId, DepoisDoEndosso).DeveFalharCom("Sinistro.IndenizacaoAcimaDoLimite");
        anterior.Aprovar(100_000m, null, Cenarios.AnalistaId, DepoisDoEndosso).DeveTerSucesso();
    }

    [Fact]
    public void Deve_aceitar_quando_cobertura_foi_excluida_depois_da_ocorrencia()
    {
        var apolice = Cenarios.ApoliceAuto(Cenarios.Agora, coberturas: [.. Cenarios.CoberturasAutoBasicas(), new("VIDROS", null)]);
        Endossar(apolice, new(TipoEndosso.ExclusaoCobertura, "VIDROS"), DiaDoEndosso);

        var sinistro = Abrir(apolice, "VIDROS", AntesDoEndosso).DeveTerSucesso();

        (sinistro.CoberturaCodigo, sinistro.CoberturaNome, sinistro.LmiCobertura).ShouldBe(("VIDROS", "Vidros, faróis e retrovisores", 3_000m));
        Abrir(apolice, "VIDROS", new DateOnly(2026, 9, 12)).DeveFalharCom("Sinistro.CoberturaNaoContratada");
    }

    [Fact]
    public void Deve_manter_o_servico_sem_indenizacao_quando_excluido_depois_da_ocorrencia()
    {
        var apolice = ApoliceComAssistencia();
        Endossar(apolice, new(TipoEndosso.ExclusaoCobertura, "ASSISTENCIA_24H"), DiaDoEndosso);

        Abrir(apolice, "ASSISTENCIA_24H", AntesDoEndosso).DeveFalharCom("Sinistro.CoberturaSemIndenizacao");
    }

    [Fact]
    public void Deve_reconstituir_o_lmi_de_cada_periodo_quando_ha_varios_endossos_no_mesmo_item()
    {
        var apolice = ApoliceComAssistencia();
        Endossar(apolice, new(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m), Cenarios.Em(2026, 6, 10));
        Endossar(apolice, new(TipoEndosso.AlteracaoLmi, "RCF_DM", 500_000m), DiaDoEndosso);

        apolice.CoberturaEm("RCF_DM", AntesDoEndosso)!.Lmi.ShouldBe(100_000m);
        apolice.CoberturaEm("RCF_DM", new DateOnly(2026, 7, 15))!.Lmi.ShouldBe(200_000m);
        apolice.CoberturaEm("RCF_DM", new DateOnly(2026, 9, 20)).ShouldBe(
            new CoberturaVigente("RCF_DM", "RC danos materiais a terceiros", 500_000m, PermiteSinistro: true));
    }

    [Fact]
    public void Deve_existir_so_entre_inclusao_e_exclusao_quando_item_entrou_e_saiu_por_endosso()
    {
        var apolice = ApoliceComAssistencia();
        Endossar(apolice, new(TipoEndosso.InclusaoCobertura, "VIDROS"), DiaDoEndosso);
        Endossar(apolice, new(TipoEndosso.ExclusaoCobertura, "VIDROS"), Cenarios.Em(2026, 10, 10));

        apolice.CoberturaEm("VIDROS", AntesDoEndosso).ShouldBeNull();
        apolice.CoberturaEm("VIDROS", new DateOnly(2026, 9, 20))
            .ShouldBe(new CoberturaVigente("VIDROS", "Vidros, faróis e retrovisores", 3_000m, PermiteSinistro: true));
        apolice.CoberturaEm("VIDROS", new DateOnly(2026, 10, 15)).ShouldBeNull();
    }

    [Fact]
    public void Deve_devolver_o_estado_atual_quando_item_sem_endossos_ou_inexistente()
    {
        var apolice = ApoliceComAssistencia();

        apolice.CoberturaEm("CASCO", AntesDoEndosso).ShouldBe(new CoberturaVigente("CASCO", "Colisão, incêndio e roubo", 100_000m, true));
        apolice.CoberturaEm("XYZ", AntesDoEndosso).ShouldBeNull();
        apolice.CoberturaEm(null, AntesDoEndosso).ShouldBeNull();
    }

    [Fact]
    public void Deve_lancar_quando_endosso_persistido_sem_lmi_anterior()
    {
        var apolice = ApoliceComAssistencia();
        var endosso = Endossar(apolice, new(TipoEndosso.ExclusaoCobertura, "ASSISTENCIA_24H"), DiaDoEndosso);

        // Simula a linha corrompida que o EF materializaria sem passar pelo domínio.
        typeof(Endosso).GetProperty(nameof(Endosso.LmiAnterior))!.SetValue(endosso, null);

        Should.Throw<DadoPersistidoInvalidoException>(() => apolice.CoberturaEm("ASSISTENCIA_24H", AntesDoEndosso));
    }

    private static ApoliceSeguro ApoliceComAssistencia() =>
        Cenarios.ApoliceAuto(Cenarios.Agora, coberturas: Cenarios.CoberturasAutoComAssistencia());

    private static Endosso Endossar(ApoliceSeguro apolice, SolicitacaoEndosso solicitacao, DateTimeOffset agora) =>
        apolice.Endossar(solicitacao, CatalogoPadrao.Auto(), Tarifador, Cenarios.AnalistaId, agora).DeveTerSucesso();

    private static Result<Sinistro> Abrir(ApoliceSeguro apolice, string cobertura, DateOnly ocorrencia, decimal valor = 1_500m) =>
        Sinistro.Abrir(apolice, "SIN-2026-000010", cobertura, ocorrencia, Descricao, valor, Cenarios.CorretorId, DepoisDoEndosso);
}

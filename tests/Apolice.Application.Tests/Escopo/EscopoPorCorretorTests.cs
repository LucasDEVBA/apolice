using Apolice.Application.Features.Apolices.Cancelar;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Apolices.Endossar;
using Apolice.Application.Features.Apolices.GerarDocumento;
using Apolice.Application.Features.Apolices.Listar;
using Apolice.Application.Features.Apolices.Obter;
using Apolice.Application.Features.Apolices.SimularCancelamento;
using Apolice.Application.Features.Apolices.SimularEndosso;
using Apolice.Application.Features.Cotacoes.Aceitar;
using Apolice.Application.Features.Cotacoes.Obter;
using Apolice.Application.Features.Cotacoes.Recusar;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.IniciarAnalise;
using Apolice.Application.Features.Sinistros.Negar;
using Apolice.Application.Features.Sinistros.Obter;
using Apolice.Application.Features.Sinistros.RegistrarPagamento;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Escopo;

// Anti-BOLA/IDOR: recurso de outro corretor responde exatamente como recurso inexistente, sem efeito colateral.
public sealed class EscopoPorCorretorTests : TesteBase
{
    private static readonly Dictionary<string, Func<Ambiente, Recursos, Task<Result>>> Operacoes = new()
    {
        ["ObterCotacao"] = async (a, r) => await a.Servico<ObterCotacaoHandler>().HandleAsync(new ObterCotacaoQuery(r.CotacaoCalculada), Ambiente.Ct),
        ["AceitarCotacao"] = async (a, r) => await a.Servico<AceitarCotacaoHandler>().HandleAsync(new AceitarCotacaoCommand(r.CotacaoCalculada), Ambiente.Ct),
        ["RecusarCotacao"] = async (a, r) => await a.Servico<RecusarCotacaoHandler>().HandleAsync(new RecusarCotacaoCommand(r.CotacaoCalculada), Ambiente.Ct),
        ["EmitirApolice"] = async (a, r) => await a.Servico<EmitirApoliceHandler>().HandleAsync(new EmitirApoliceCommand(r.CotacaoAceita, a.Hoje, 1), Ambiente.Ct),
        ["ObterApolice"] = async (a, r) => await a.Servico<ObterApoliceHandler>().HandleAsync(new ObterApoliceQuery(r.Apolice), Ambiente.Ct),
        ["GerarDocumento"] = async (a, r) => await a.Servico<GerarDocumentoApoliceHandler>().HandleAsync(new GerarDocumentoApoliceQuery(r.Apolice), Ambiente.Ct),
        ["SimularEndosso"] = async (a, r) => await a.Servico<SimularEndossoHandler>().HandleAsync(new SimularEndossoCommand(r.Apolice, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ambiente.Ct),
        ["Endossar"] = async (a, r) => await a.Servico<EndossarApoliceHandler>().HandleAsync(new EndossarApoliceCommand(r.Apolice, TipoEndosso.InclusaoCobertura, CodigosCobertura.Vidros, null), Ambiente.Ct),
        ["SimularCancelamento"] = async (a, r) => await a.Servico<SimularCancelamentoHandler>().HandleAsync(new SimularCancelamentoCommand(r.Apolice, MotivoCancelamento.PedidoDoSegurado), Ambiente.Ct),
        ["Cancelar"] = async (a, r) => await a.Servico<CancelarApoliceHandler>().HandleAsync(new CancelarApoliceCommand(r.Apolice, MotivoCancelamento.PedidoDoSegurado), Ambiente.Ct),
        ["AbrirSinistro"] = async (a, r) => await a.Servico<AbrirSinistroHandler>().HandleAsync(new AbrirSinistroCommand(r.Apolice, CodigosCobertura.Casco, a.Hoje, Ambiente.DescricaoSinistro, 100m), Ambiente.Ct),
        ["ObterSinistro"] = async (a, r) => await a.Servico<ObterSinistroHandler>().HandleAsync(new ObterSinistroQuery(r.Sinistro), Ambiente.Ct),
        ["IniciarAnalise"] = async (a, r) => await a.Servico<IniciarAnaliseSinistroHandler>().HandleAsync(new IniciarAnaliseSinistroCommand(r.Sinistro), Ambiente.Ct),
        ["AprovarSinistro"] = async (a, r) => await a.Servico<AprovarSinistroHandler>().HandleAsync(new AprovarSinistroCommand(r.Sinistro, 10m, null), Ambiente.Ct),
        ["NegarSinistro"] = async (a, r) => await a.Servico<NegarSinistroHandler>().HandleAsync(new NegarSinistroCommand(r.Sinistro, "Motivo suficientemente longo."), Ambiente.Ct),
        ["PagarSinistro"] = async (a, r) => await a.Servico<RegistrarPagamentoSinistroHandler>().HandleAsync(new RegistrarPagamentoSinistroCommand(r.Sinistro), Ambiente.Ct),
    };

    public static TheoryData<string> TodasAsOperacoes => [.. Operacoes.Keys];

    public static TheoryData<string> Leituras => ["ObterCotacao", "ObterApolice", "GerarDocumento", "ObterSinistro"];

    [Theory]
    [MemberData(nameof(TodasAsOperacoes))]
    public async Task Deve_retornar_nao_encontrado_sem_efeito_quando_corretor_acessa_recurso_alheio(string operacao)
    {
        var recursos = await PrepararCarteiraDaAnaAsync();
        var savesAntes = Amb.Banco.TotalSaves;
        var numeradoresAntes = Amb.Banco.Numeradores.ToDictionary();
        Amb.Como(Amb.Bruno);

        var resultado = await Operacoes[operacao](Amb, recursos);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
        Amb.Banco.TotalSaves.ShouldBe(savesAntes);
        Amb.Banco.Numeradores.ShouldBe(numeradoresAntes);
        Amb.Banco.Cotacoes.Single(c => c.Id == recursos.CotacaoCalculada).Status.ShouldBe(StatusCotacao.Calculada);
        Amb.Banco.Apolices.Single().Status.ShouldBe(StatusApolice.Ativa);
        Amb.Banco.Sinistros.Single().Status.ShouldBe(StatusSinistro.Aberto);
    }

    [Theory]
    [MemberData(nameof(TodasAsOperacoes))]
    public async Task Deve_responder_igual_a_recurso_inexistente_quando_id_nao_existe(string operacao)
    {
        await PrepararCarteiraDaAnaAsync();
        var inexistentes = new Recursos(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Amb.Como(Amb.Bruno);

        var resultado = await Operacoes[operacao](Amb, inexistentes);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
    }

    [Theory]
    [MemberData(nameof(Leituras))]
    public async Task Deve_enxergar_qualquer_carteira_quando_analista_ou_admin_le_por_id(string operacao)
    {
        var recursos = await PrepararCarteiraDaAnaAsync();

        Amb.Como(Amb.Carla);
        var analista = await Operacoes[operacao](Amb, recursos);
        Amb.Como(Amb.Diego);
        var admin = await Operacoes[operacao](Amb, recursos);
        Amb.Como(Amb.Ana);
        var dona = await Operacoes[operacao](Amb, recursos);

        analista.IsSuccess.ShouldBeTrue();
        admin.IsSuccess.ShouldBeTrue();
        dona.IsSuccess.ShouldBeTrue();
        Amb.Consultas.CorretoresConsultados.TakeLast(3).ShouldBe([null, null, Amb.Ana.Id]);
    }

    // Autenticação antes de procurar: senão o anônimo distinguiria id existente (401) de inexistente (404).
    [Theory]
    [MemberData(nameof(TodasAsOperacoes))]
    public async Task Deve_exigir_autenticacao_antes_de_procurar_o_recurso_quando_anonimo(string operacao)
    {
        await PrepararCarteiraDaAnaAsync();
        var inexistentes = new Recursos(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Amb.UsuarioAtual.Anonimo();

        var resultado = await Operacoes[operacao](Amb, inexistentes);

        resultado.DeveFalharCom("Auth.NaoAutenticado", ErrorType.NaoAutorizado);
    }

    // Defesa em profundidade: um WHERE esquecido na consulta da infra não entrega o registro (nem o PDF com CPF
    // completo) de outra carteira, porque o handler confere o CorretorId do que voltou.
    [Theory]
    [InlineData("ObterCotacao")]
    [InlineData("ObterApolice")]
    [InlineData("GerarDocumento")]
    public async Task Deve_negar_registro_de_outra_carteira_mesmo_quando_a_consulta_esquece_o_filtro(string operacao)
    {
        var recursos = await PrepararCarteiraDaAnaAsync();
        Amb.Consultas.IgnorarFiltroDeCorretor = true;
        Amb.Como(Amb.Bruno);

        var resultado = await Operacoes[operacao](Amb, recursos);

        resultado.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
        Amb.Gerador.UltimaApolice.ShouldBeNull();
    }

    // A visão global sai do Papel: uma implementação de ICurrentUser que derive PodeVerTudo por outra regra
    // (ex.: verdadeiro para corretor) não abre a carteira dos outros.
    [Fact]
    public async Task Deve_restringir_a_propria_carteira_quando_usuario_atual_informa_pode_ver_tudo_por_engano()
    {
        var recursos = await PrepararCarteiraDaAnaAsync();
        Amb.Como(Amb.Bruno);
        Amb.UsuarioAtual.PodeVerTudoForcado = true;

        var obter = await Operacoes["ObterApolice"](Amb, recursos);
        var cancelar = await Operacoes["Cancelar"](Amb, recursos);
        var listar = await Amb.Servico<ListarApolicesHandler>().HandleAsync(new ListarApolicesQuery(), Ambiente.Ct);

        obter.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
        cancelar.DeveFalharCom("Comum.NaoEncontrado", ErrorType.NaoEncontrado);
        listar.DeveTerSucesso().Itens.ShouldBeEmpty();
        Amb.Consultas.UltimoFiltroApolices!.CorretorId.ShouldBe(Amb.Bruno.Id);
    }

    // Admin opera qualquer carteira (política Corretor = Corretor|Admin, SPEC 7); o registro continua do corretor dono.
    [Fact]
    public async Task Deve_permitir_que_o_admin_endosse_avise_sinistro_e_cancele_na_carteira_alheia()
    {
        var recursos = await PrepararCarteiraDaAnaAsync();
        Amb.Como(Amb.Diego);

        var endosso = await Operacoes["Endossar"](Amb, recursos);
        var sinistro = await Operacoes["AbrirSinistro"](Amb, recursos);
        var cancelamento = await Operacoes["Cancelar"](Amb, recursos);

        endosso.IsSuccess.ShouldBeTrue();
        sinistro.IsSuccess.ShouldBeTrue();
        cancelamento.IsSuccess.ShouldBeTrue();
        var apolice = Amb.Banco.Apolices.Single();
        apolice.Status.ShouldBe(StatusApolice.Cancelada);
        apolice.CorretorId.ShouldBe(Amb.Ana.Id);
        apolice.Endossos.ShouldHaveSingleItem().CriadoPorId.ShouldBe(Amb.Diego.Id);
        Amb.Banco.Sinistros.Count.ShouldBe(2);
        Amb.Banco.Sinistros.ShouldAllBe(s => s.CorretorId == Amb.Ana.Id);
    }

    private async Task<Recursos> PrepararCarteiraDaAnaAsync()
    {
        var calculada = await Amb.CriarCotacaoAsync(Amb.Ana);
        var aceita = await Amb.CotacaoAceitaAsync(Amb.Ana);
        var apolice = await Amb.EmitirApoliceAsync(Amb.Ana);
        var sinistro = await Amb.AbrirSinistroAsync(Amb.Ana, apolice.Id);
        return new Recursos(calculada.Id, aceita.Id, apolice.Id, sinistro.Id);
    }

    public sealed record Recursos(Guid CotacaoCalculada, Guid CotacaoAceita, Guid Apolice, Guid Sinistro);
}

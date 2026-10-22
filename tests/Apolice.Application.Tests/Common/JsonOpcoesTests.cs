using System.Text.Json;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Common;
using Apolice.Application.Features.Cotacoes;
using Apolice.Domain.Apolices;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Tests.Common;

public sealed class JsonOpcoesTests
{
    [Fact]
    public void Deve_serializar_camel_case_enum_texto_e_dateonly_iso_quando_usa_opcoes_padrao()
    {
        var json = JsonSerializer.Serialize(new { MotivoCancelamento = MotivoCancelamento.PedidoDoSegurado, Data = new DateOnly(2026, 3, 10) }, JsonOpcoes.Padrao);

        json.ShouldBe("""{"motivoCancelamento":"PedidoDoSegurado","data":"2026-03-10"}""");
    }

    [Fact]
    public void Deve_desserializar_dados_de_risco_polimorficos_quando_discriminador_nao_vem_primeiro()
    {
        const string json = """
            {"produtoCodigo":"RESID","segurado":null,"coberturas":[],
             "dadosRisco":{"valorImovel":800000,"tipoImovel":"Apartamento","cep":"01310-100","habitual":true,"produto":"RESID"}}
            """;

        var request = JsonSerializer.Deserialize<CotacaoRequest>(json, JsonOpcoes.Padrao)!;

        request.DadosRisco.ShouldBe(new DadosRiscoResidencial(800_000m, TipoImovel.Apartamento, "01310-100", true));
    }

    [Fact]
    public void Deve_preservar_o_evento_id_quando_evento_vai_e_volta_pelo_outbox()
    {
        var evento = new ApoliceCancelada(Guid.NewGuid(), "AUTO-2026-000001", MotivoCancelamento.Inadimplencia, 10.5m, "Maria", "m@x.com", DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(evento, JsonOpcoes.Padrao);
        var copia = JsonSerializer.Deserialize<ApoliceCancelada>(json, JsonOpcoes.Padrao)!;

        copia.ShouldBe(evento);
        copia.EventoId.ShouldBe(evento.EventoId);
        json.ShouldContain("\"motivo\":\"Inadimplencia\"");
    }

    [Fact]
    public void Deve_recusar_enum_numerico_e_permitir_aplicar_as_regras_em_outras_opcoes()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<CancelamentoTeste>("""{"motivo":1}""", JsonOpcoes.Padrao));

        var opcoesDaApi = new JsonSerializerOptions();
        JsonOpcoes.Configurar(opcoesDaApi);
        opcoesDaApi.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        JsonOpcoes.Padrao.IsReadOnly.ShouldBeTrue();
        Should.Throw<ArgumentNullException>(() => JsonOpcoes.Configurar(null!));
    }

    [Fact]
    public void Deve_montar_as_chaves_canonicas_quando_numera_documentos()
    {
        ChavesNumerador.Apolice("AUTO", 2026).ShouldBe("APOLICE:AUTO:2026");
        ChavesNumerador.Sinistro(2026).ShouldBe("SINISTRO:2026");
    }

    private sealed record CancelamentoTeste(MotivoCancelamento Motivo);
}

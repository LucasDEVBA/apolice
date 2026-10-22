using Apolice.Application.Common;
using Apolice.Application.Features.Sinistros;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Common;

public sealed class MapeamentosTests : TesteBase
{
    // O domínio admite abertura sem autor humano (usuarioId nulo): o histórico mostra a ação sem nome, e não "desconhecido".
    [Fact]
    public async Task Deve_deixar_o_nome_vazio_no_historico_quando_acao_nao_tem_autor_humano()
    {
        await Amb.EmitirApoliceAsync(Amb.Ana);
        var apolice = Amb.Banco.Apolices.Single();
        var sinistro = Sinistro.Abrir(apolice, "SIN-2026-000001", CodigosCobertura.Casco, Amb.Hoje, Ambiente.DescricaoSinistro, 100m, null, Amb.Agora).Value;

        var dto = SinistroMapeamento.ParaDto(sinistro, "Ana Ribeiro", new Dictionary<Guid, string>());

        dto.Historico.ShouldHaveSingleItem().UsuarioNome.ShouldBeNull();
    }

    [Fact]
    public void Deve_lancar_quando_releitura_nao_acha_o_que_acabou_de_ser_gravado()
    {
        Should.Throw<InvalidOperationException>(() => Releitura.Obrigatoria<string>(null, Entidades.Apolice));
        Releitura.Obrigatoria("ok", Entidades.Apolice).ShouldBe("ok");
    }
}

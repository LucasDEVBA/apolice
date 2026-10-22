using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Common;

public sealed class AcessoUsuarioTests
{
    private static readonly Guid Dono = Guid.CreateVersion7();

    [Theory]
    [InlineData(Papel.Corretor, false)]
    [InlineData(Papel.Analista, true)]
    [InlineData(Papel.Admin, true)]
    [InlineData(null, false)]
    public void Deve_derivar_a_visao_global_do_papel_quando_infra_nao_implementa_pode_ver_tudo(Papel? papel, bool esperado)
    {
        ICurrentUser usuario = new UsuarioMinimo(Guid.CreateVersion7(), papel);

        usuario.PodeVerTudo.ShouldBe(esperado);
        AcessoUsuario.TemVisaoGlobal(papel).ShouldBe(esperado);
    }

    // Autenticado sem papel (claim de role ausente ou mal mapeada): nunca vira visão global nem permissão.
    [Fact]
    public void Deve_restringir_e_proibir_quando_usuario_autenticado_nao_tem_papel()
    {
        var id = Guid.CreateVersion7();
        ICurrentUser usuario = new UsuarioMinimo(id, null);

        usuario.Escopo().DeveTerSucesso().ShouldBe(new EscopoCorretor(id));
        usuario.ExigirPapel(Papel.Corretor, Papel.Admin).DeveFalharCom(ComumErros.Proibido.Codigo, ErrorType.Proibido);
        usuario.AutorizarCarteira(id, Entidades.Cotacao).DeveFalharCom(ComumErros.Proibido.Codigo);
    }

    [Fact]
    public void Deve_exigir_autenticacao_quando_regras_de_carteira_e_analise_sao_usadas_por_anonimo()
    {
        ICurrentUser anonimo = new UsuarioMinimo(null, Papel.Admin);

        anonimo.AutorizarCarteira(Dono, Entidades.Apolice).DeveFalharCom(ComumErros.NaoAutenticado.Codigo, ErrorType.NaoAutorizado);
        anonimo.AutorizarAnalise(Dono, Entidades.Sinistro).DeveFalharCom(ComumErros.NaoAutenticado.Codigo);
        anonimo.ExigirAutenticado().DeveFalharCom(ComumErros.NaoAutenticado.Codigo);
    }

    [Fact]
    public void Deve_alcancar_so_a_propria_carteira_quando_escopo_nao_e_global()
    {
        var proprio = new EscopoCorretor(Dono);

        proprio.Alcanca(Dono).ShouldBeTrue();
        proprio.Alcanca(Guid.CreateVersion7()).ShouldBeFalse();
        EscopoCorretor.Global.Alcanca(Dono).ShouldBeTrue();
        EscopoCorretor.Global.EhGlobal.ShouldBeTrue();
    }

    // Implementação mínima, como a infra pode escrever: sem PodeVerTudo (usa o membro padrão da interface).
    private sealed class UsuarioMinimo(Guid? id, Papel? papel) : ICurrentUser
    {
        public Guid? UsuarioId { get; } = id;

        public string? Nome => null;

        public Papel? Papel { get; } = papel;

        public bool Autenticado => UsuarioId is not null;
    }
}

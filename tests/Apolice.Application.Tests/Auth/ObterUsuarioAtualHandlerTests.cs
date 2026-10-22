using Apolice.Application.Common;
using Apolice.Application.Features.Auth;
using Apolice.Application.Features.Auth.Me;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Tests.Auth;

public sealed class ObterUsuarioAtualHandlerTests : TesteBase
{
    private ObterUsuarioAtualHandler Handler => Amb.Servico<ObterUsuarioAtualHandler>();

    [Fact]
    public async Task Deve_devolver_o_usuario_logado_quando_autenticado()
    {
        Amb.Como(Amb.Carla);

        var resultado = await Handler.HandleAsync(new ObterUsuarioAtualQuery(), Ct);

        resultado.DeveTerSucesso().ShouldBe(new UsuarioDto(Amb.Carla.Id, "Carla Souza", "carla.analista@apolice.dev", Papel.Analista));
    }

    [Fact]
    public async Task Deve_retornar_nao_autenticado_quando_anonimo_desativado_ou_removido()
    {
        Amb.UsuarioAtual.Anonimo();
        var anonimo = await Handler.HandleAsync(new ObterUsuarioAtualQuery(), Ct);

        Amb.Como(Amb.Bruno);
        Reflexao.Definir(Amb.Bruno, nameof(Usuario.Ativo), false);
        var desativado = await Handler.HandleAsync(new ObterUsuarioAtualQuery(), Ct);

        Amb.Como(Amb.Ana);
        Amb.Banco.Usuarios.Remove(Amb.Ana);
        var removido = await Handler.HandleAsync(new ObterUsuarioAtualQuery(), Ct);

        anonimo.DeveFalharCom(ComumErros.NaoAutenticado.Codigo, ErrorType.NaoAutorizado);
        desativado.DeveFalharCom(ComumErros.NaoAutenticado.Codigo);
        removido.DeveFalharCom(ComumErros.NaoAutenticado.Codigo);
    }
}

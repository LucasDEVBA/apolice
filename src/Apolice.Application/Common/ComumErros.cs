using Apolice.Domain.Comum;

namespace Apolice.Application.Common;

public static class ComumErros
{
    // Transversal a todos os handlers (por isso aqui, e não em AuthErros); o código continua "Auth.*" porque é contrato
    // com o frontend (type = urn:apolice:problema:Auth.NaoAutenticado).
    public static Error NaoAutenticado { get; } =
        Error.NaoAutorizado("Auth.NaoAutenticado", "Autentique-se para continuar.");

    // Mesma resposta para "não existe" e "é de outro corretor": não revela a existência do recurso (anti-BOLA).
    public static Error NaoEncontrado(string entidade) =>
        Error.NaoEncontrado("Comum.NaoEncontrado", $"Nenhum registro de {entidade} foi encontrado com o identificador informado.");

    public static Error Proibido { get; } =
        Error.Proibido("Comum.Proibido", "Seu perfil não tem permissão para esta operação.");

    // 404, como o FeatureGate do Microsoft.FeatureManagement: recurso desligado se comporta como inexistente.
    public static Error FuncionalidadeDesabilitada { get; } =
        Error.NaoEncontrado("Comum.FuncionalidadeDesabilitada", "Esta funcionalidade está desabilitada no momento.");

    public static Error CursorInvalido { get; } =
        Error.Validacao("Comum.CursorInvalido", "Cursor de paginação inválido.");

    public static Error DadosObrigatorios(string campo) =>
        Error.Validacao("Comum.DadosObrigatorios", $"Informe {campo}.");

    public static Error LimiteInvalido(int minimo, int maximo) =>
        Error.Validacao("Comum.LimiteInvalido", $"O limite deve estar entre {minimo} e {maximo}.");
}

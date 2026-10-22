using System.Text.Json;

namespace Apolice.Application.Common;

// Detalhes da auditoria em JSON (legível por máquina); nunca incluir CPF, e-mail, senha ou token.
internal static class DetalhesAuditoria
{
    public static string Json<T>(T detalhes) => JsonSerializer.Serialize(detalhes, JsonOpcoes.Padrao);
}

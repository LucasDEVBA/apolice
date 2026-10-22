using System.Reflection;

namespace Apolice.Application.Tests.Suporte;

internal static class Reflexao
{
    // O domínio não expõe desativação de usuário (só o cadastro/admin da infra faria isso): o teste força o estado.
    public static void Definir(object alvo, string propriedade, object? valor)
    {
        var info = alvo.GetType().GetProperty(propriedade, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Propriedade {propriedade} não encontrada.");
        info.SetValue(alvo, valor);
    }
}

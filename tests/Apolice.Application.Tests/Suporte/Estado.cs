using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Apolice.Domain.Comum;

namespace Apolice.Application.Tests.Suporte;

// Cópia e comparação estruturais dos agregados por reflexão (campos privados inclusive), para o banco em memória
// separar o estado GRAVADO do estado em edição numa sessão — como o banco real separa linha confirmada de entidade
// rastreada pelo DbContext. Records são imutáveis e compartilhados; listas e entidades filhas são copiadas.
internal static class Estado
{
    private const string CampoEventos = "_domainEvents";
    private const string CampoVersao = "<Versao>k__BackingField";
    private const BindingFlags CamposDeclarados = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // O clone nasce sem eventos: estado gravado não carrega eventos pendentes.
    public static T Clonar<T>(T origem)
        where T : class
    {
        var clone = (T)ClonarValor(origem)!;
        LimparEventos(clone);
        return clone;
    }

    public static void Copiar(object origem, object destino)
    {
        foreach (var campo in Campos(origem.GetType()))
        {
            campo.SetValue(destino, ClonarValor(campo.GetValue(origem)));
        }
    }

    // Igualdade do estado persistível: eventos pendentes e a versão (controlada pelo banco) não contam.
    public static bool Iguais(object a, object b) => ValoresIguais(a, b);

    public static int Versao(object agregado) => (int)CampoDeVersao(agregado).GetValue(agregado)!;

    public static void DefinirVersao(object agregado, int versao) => CampoDeVersao(agregado).SetValue(agregado, versao);

    private static void LimparEventos(object instancia)
    {
        if (instancia is AggregateRoot<Guid> agregado)
        {
            agregado.ClearDomainEvents();
        }
        else if (instancia is AggregateRoot<string> agregadoPorCodigo)
        {
            agregadoPorCodigo.ClearDomainEvents();
        }
    }

    private static FieldInfo CampoDeVersao(object agregado) =>
        Campos(agregado.GetType()).FirstOrDefault(c => c.Name == CampoVersao)
            ?? throw new InvalidOperationException($"{agregado.GetType().Name} não é agregado versionado.");

    private static object? ClonarValor(object? valor)
    {
        if (valor is null || Imutavel(valor.GetType()))
        {
            return valor;
        }

        if (valor is IList lista && EhLista(valor.GetType()))
        {
            var copia = (IList)Activator.CreateInstance(valor.GetType())!;
            foreach (var item in lista)
            {
                copia.Add(ClonarValor(item));
            }

            return copia;
        }

        if (valor is IEnumerable)
        {
            throw new NotSupportedException($"Coleção {valor.GetType().Name} não suportada pela cópia de estado.");
        }

        var clone = RuntimeHelpers.GetUninitializedObject(valor.GetType());
        Copiar(valor, clone);
        return clone;
    }

    private static bool ValoresIguais(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.GetType() != b.GetType())
        {
            return false;
        }

        if (Imutavel(a.GetType()))
        {
            return a.Equals(b);
        }

        if (a is IList listaA && b is IList listaB)
        {
            return listaA.Count == listaB.Count && Enumerable.Range(0, listaA.Count).All(i => ValoresIguais(listaA[i], listaB[i]));
        }

        return Campos(a.GetType())
            .Where(c => c.Name is not (CampoEventos or CampoVersao))
            .All(c => ValoresIguais(c.GetValue(a), c.GetValue(b)));
    }

    private static bool Imutavel(Type tipo) =>
        tipo.IsValueType || tipo == typeof(string) || tipo.GetMethod("<Clone>$") is not null;

    private static bool EhLista(Type tipo) => tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(List<>);

    private static IEnumerable<FieldInfo> Campos(Type tipo)
    {
        for (var atual = tipo; atual is not null && atual != typeof(object); atual = atual.BaseType)
        {
            foreach (var campo in atual.GetFields(CamposDeclarados))
            {
                yield return campo;
            }
        }
    }
}
